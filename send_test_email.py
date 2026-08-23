import pythoncom
import win32com.client

def send_test_email():
    pythoncom.CoInitialize()
    try:
        outlook = win32com.client.Dispatch("Outlook.Application")
        namespace = outlook.GetNamespace("MAPI")
        
        # Create unread email directly in Inbox
        inbox = namespace.GetDefaultFolder(6) # 6 = olFolderInbox
        msg = inbox.Items.Add(0) # 0 = olMailItem
        
        msg.Subject = "Request: Last 5 Patients Information AI response"
        msg.Body = "Hello Agent, Please provide last 5 patients information AI response from MySQL labreports database."
        msg.UnRead = True
        msg.Save()
        
        print(f"[SendTestEmail] Created unread test email in desktop Outlook Inbox! Subject: '{msg.Subject}'")
    except Exception as e:
        print(f"[SendTestEmail Error] {e}")
    finally:
        pythoncom.CoUninitialize()

if __name__ == "__main__":
    send_test_email()
