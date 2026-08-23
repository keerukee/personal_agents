import os
import pythoncom
import win32com.client

STAGING_DIR = r"C:\temp\agent_staging"

class OutlookClient:
    def __init__(self):
        os.makedirs(STAGING_DIR, exist_ok=True)

    def fetch_unread_emails(self):
        """Connects to local MAPI namespace and reads unread emails."""
        pythoncom.CoInitialize()
        emails = []
        try:
            outlook = win32com.client.Dispatch("Outlook.Application")
            namespace = outlook.GetNamespace("MAPI")
            inbox = namespace.GetDefaultFolder(6) # 6 = olFolderInbox
            messages = inbox.Items.Restrict("[UnRead] = true")

            for msg in messages:
                attachments = []
                if msg.Attachments.Count > 0:
                    for i in range(1, msg.Attachments.Count + 1):
                        att = msg.Attachments.Item(i)
                        save_path = os.path.join(STAGING_DIR, att.FileName)
                        att.SaveAsFile(save_path)
                        attachments.append({
                            "fileName": att.FileName,
                            "localFilePath": save_path
                        })

                emails.append({
                    "entryId": msg.EntryID,
                    "sender": str(msg.SenderEmailAddress),
                    "senderName": str(msg.SenderName),
                    "subject": str(msg.Subject),
                    "bodyText": str(msg.Body),
                    "attachments": attachments
                })
                # Mark as read after reading
                msg.UnRead = False
                msg.Save()

        except Exception as e:
            print(f"[OutlookClient Error] {e}")
        finally:
            pythoncom.CoUninitialize()

        return emails

    def send_or_reply_email(self, target_entry_id: str = None, to: str = None, subject: str = "Automated Response", html_body: str = "", attachments: list = None):
        """Replies to existing email thread or sends new email via desktop Outlook MAPI."""
        pythoncom.CoInitialize()
        try:
            outlook = win32com.client.Dispatch("Outlook.Application")
            namespace = outlook.GetNamespace("MAPI")

            mail = None
            if target_entry_id:
                try:
                    orig_item = namespace.GetItemFromID(target_entry_id)
                    if orig_item:
                        mail = orig_item.Reply()
                except Exception as ex:
                    print(f"[OutlookClient Info] Could not reply to EntryID, creating new mail: {ex}")
                    mail = None

            if mail is None:
                mail = outlook.CreateItem(0)

            recipient = to or "keerukee@outlook.com"
            mail.To = recipient
            mail.Subject = subject
            mail.HTMLBody = html_body

            if attachments:
                for att in attachments:
                    if att and os.path.isfile(att):
                        mail.Attachments.Add(att)

            mail.Send()
            safe_subj = subject.encode('ascii', 'replace').decode('ascii')
            print(f"[OutlookClient Success] Sent email to '{recipient}' with subject: '{safe_subj}'")
            return True
        except Exception as e:
            print(f"[OutlookClient Error] Failed to send email: {e}")
            raise e
        finally:
            pythoncom.CoUninitialize()
