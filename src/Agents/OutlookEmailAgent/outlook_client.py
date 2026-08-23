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

    def send_or_reply_email(self, target_entry_id: str, subject: str, html_body: str, attachments: list = None):
        """Replies to existing email thread or sends new email via desktop Outlook MAPI."""
        pythoncom.CoInitialize()
        try:
            outlook = win32com.client.Dispatch("Outlook.Application")
            namespace = outlook.GetNamespace("MAPI")

            if target_entry_id:
                try:
                    orig_item = namespace.GetItemFromID(target_entry_id)
                    mail = orig_item.Reply()
                except Exception:
                    mail = outlook.CreateItem(0) # 0 = olMailItem
            else:
                mail = outlook.CreateItem(0)

            mail.Subject = subject
            mail.HTMLBody = html_body + "<br/><br/>" + getattr(mail, "HTMLBody", "")

            if attachments:
                for att in attachments:
                    if os.path.exists(att):
                        mail.Attachments.Add(att)

            mail.Send()
            print(f"[OutlookClient Success] Sent email: '{subject}'")
            return True
        except Exception as e:
            print(f"[OutlookClient Error] Failed to send email: {e}")
            raise e
        finally:
            pythoncom.CoUninitialize()
