import time
import sqlite3
import os
from outlook_client import OutlookClient
from db_queue import enqueue_inbound_event, fetch_pending_tasks, claim_task, complete_task

LEDGER_DB = "processed_emails.db"

def init_ledger():
    conn = sqlite3.connect(LEDGER_DB)
    cursor = conn.cursor()
    cursor.execute("""
        CREATE TABLE IF NOT EXISTS processed_ledger (
            entry_id TEXT PRIMARY KEY,
            processed_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
        )
    """)
    conn.commit()
    conn.close()

def is_processed(entry_id: str) -> bool:
    conn = sqlite3.connect(LEDGER_DB)
    cursor = conn.cursor()
    cursor.execute("SELECT 1 FROM processed_ledger WHERE entry_id = ?", (entry_id,))
    row = cursor.fetchone()
    conn.close()
    return row is not None

def mark_processed(entry_id: str):
    conn = sqlite3.connect(LEDGER_DB)
    cursor = conn.cursor()
    cursor.execute("INSERT OR IGNORE INTO processed_ledger (entry_id) VALUES (?)", (entry_id,))
    conn.commit()
    conn.close()

def run_inbox_watcher(client: OutlookClient):
    """Reads unread desktop Outlook emails and enqueues them directly into SQL Server InboundEvents table."""
    try:
        emails = client.fetch_unread_emails()
        for email in emails:
            entry_id = email["entryId"]
            if is_processed(entry_id):
                continue

            subject_lower = email["subject"].lower()
            body_lower = email["bodyText"].lower()
            sender_lower = email.get("sender", "").lower()

            # Ignore no-reply automated marketing emails
            if "no-reply" in sender_lower or "noreply" in sender_lower or "donotreply" in sender_lower:
                mark_processed(entry_id)
                continue

            # Only process relevant agent request emails
            keywords = ["request", "agent", "lab report", "patient", "task", "report", "query"]
            if not any(k in subject_lower or k in body_lower for k in keywords):
                mark_processed(entry_id)
                continue

            prompt = f"Process incoming email: {email['subject']}\nBody: {email['bodyText']}"
            event_guid = enqueue_inbound_event(
                source="OutlookEmailAgent",
                prompt=prompt,
                data_json=email
            )
            safe_subject = email['subject'].encode('ascii', 'replace').decode('ascii')
            print(f"[OutlookWatcher] Enqueued email '{safe_subject}' to SQL Server InboundEvents (Guid: {event_guid})")
            mark_processed(entry_id)
    except Exception as e:
        print(f"[OutlookWatcher Error] {e}")

def run_outbound_queue_worker(client: OutlookClient):
    """Polls SQL Server AgentTasks table for outbound email tasks assigned to outlook-email-agent."""
    try:
        tasks = fetch_pending_tasks("outlook-email-agent")
        for t in tasks:
            task_guid = t["task_guid"]
            payload = t["payload"]
            
            if claim_task(task_guid):
                print(f"[OutlookWorker] Claimed outbound email task {task_guid}")
                try:
                    target_entry_id = payload.get("targetEntryId")
                    to_recipient = payload.get("to") or payload.get("sender") or "keerukee@outlook.com"
                    subject = payload.get("subject", "Automated Response")
                    html_body = payload.get("htmlBody", payload.get("prompt", "<p>Task executed successfully.</p>"))
                    attachments = payload.get("attachments")

                    if "no-reply" in to_recipient.lower() or "noreply" in to_recipient.lower() or "donotreply" in to_recipient.lower():
                        complete_task(task_guid, result_json={"status": "Skipped automated no-reply recipient"})
                        print(f"[OutlookWorker] Skipped sending to automated no-reply recipient '{to_recipient}'")
                        continue

                    client.send_or_reply_email(
                        target_entry_id=target_entry_id,
                        to=to_recipient,
                        subject=subject,
                        html_body=html_body,
                        attachments=attachments
                    )

                    complete_task(task_guid, result_json={"status": "Email sent successfully"})
                    print(f"[OutlookWorker] Completed task {task_guid}")
                except Exception as ex:
                    complete_task(task_guid, result_json=None, error_message=str(ex))
    except Exception as e:
        print(f"[OutlookWorker Error] {e}")

def main():
    print("=" * 70)
    print("  PYTHON OUTLOOK DESKTOP DISCONNECTED DATABASE QUEUE AGENT")
    print("  Mode: SQL Server Event Bus & pywin32 MAPI Automation")
    print("=" * 70)

    init_ledger()
    client = OutlookClient()

    while True:
        # 1. Sweep Outlook Inbox -> SQL Server InboundEvents
        run_inbox_watcher(client)

        # 2. Check SQL Server AgentTasks -> Send/Reply Outbound Emails
        run_outbound_queue_worker(client)

        time.sleep(3)

if __name__ == "__main__":
    main()
