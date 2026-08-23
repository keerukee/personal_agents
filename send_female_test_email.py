import pythoncom
import win32com.client

def send_female_request_email():
    pythoncom.CoInitialize()
    try:
        outlook = win32com.client.Dispatch("Outlook.Application")
        namespace = outlook.GetNamespace("MAPI")
        
        # Save an unread email directly into Outlook Inbox
        inbox = namespace.GetDefaultFolder(6) # 6 = olFolderInbox
        msg = inbox.Items.Add(0) # 0 = olMailItem
        
        msg.Subject = "Request: Last 5 Female Patient Lab Reports"
        msg.Body = "Hello Central Orchestrator, Please send me the last 5 female patient lab reports from MySQL database with reference ranges."
        msg.UnRead = True
        msg.Save()
        
        print(f"[TestTrigger] Created unread test email in desktop Outlook Inbox! Subject: '{msg.Subject}'")
    except Exception as e:
        print(f"[TestTrigger Error] {e}")
    finally:
        pythoncom.CoUninitialize()

if __name__ == "__main__":
    send_female_request_email()
