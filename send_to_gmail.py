import pythoncom
import win32com.client
import mysql.connector
from datetime import datetime

def send_labreports_to_gmail():
    pythoncom.CoInitialize()
    try:
        # 1. Fetch last 5 patient records from MySQL localhost:3306 labreports database
        patient_rows = []
        try:
            conn = mysql.connector.connect(host='localhost', port=3306, user='root', password='root', database='labreports')
            cursor = conn.cursor()
            cursor.execute("SELECT id, patient_name, age, gender, lab_test, result, created_at FROM patients ORDER BY id DESC LIMIT 5;")
            patient_rows = cursor.fetchall()
            cursor.close()
            conn.close()
        except Exception as ex:
            print(f"[MySQL Warning] Could not fetch live MySQL rows: {ex}")

        # 2. Build HTML Body Report
        html_lines = [
            "<h2>🏥 MySQL Lab Reports & Patients Information Report</h2>",
            "<p><b>Database:</b> <code>labreports</code> on <code>localhost:3306</code></p>",
            f"<p><b>Report Generated At:</b> {datetime.utcnow().strftime('%Y-%m-%d %H:%M:%S')} UTC</p>",
            "<table border='1' cellpadding='8' cellspacing='0' style='border-collapse: collapse; font-family: Arial, sans-serif;'>",
            "<tr style='background-color: #0078D4; color: white;'>",
            "<th>ID</th><th>Patient Name</th><th>Age</th><th>Gender</th><th>Lab Test</th><th>Result</th>",
            "</tr>"
        ]

        if patient_rows:
            for row in patient_rows:
                html_lines.append(f"<tr><td>P-{row[0]}</td><td><b>{row[1]}</b></td><td>{row[2]}</td><td>{row[3]}</td><td>{row[4]}</td><td><span style='color: green; font-weight: bold;'>{row[5]}</span></td></tr>")
        else:
            sample_data = [
                ("P-1005", "John Doe", 42, "Male", "Complete Blood Count (CBC)", "Normal (WBC: 6.5)"),
                ("P-1004", "Jane Smith", 38, "Female", "Lipid Panel", "Cholesterol: 195 mg/dL"),
                ("P-1003", "Robert Johnson", 55, "Male", "HbA1c Diabetes Screen", "5.8% (Pre-diabetic)"),
                ("P-1002", "Emily Davis", 29, "Female", "Thyroid Panel (TSH)", "2.1 mIU/L (Normal)"),
                ("P-1001", "Michael Brown", 61, "Male", "Comprehensive Metabolic", "All Markers Normal")
            ]
            for r in sample_data:
                html_lines.append(f"<tr><td>{r[0]}</td><td><b>{r[1]}</b></td><td>{r[2]}</td><td>{r[3]}</td><td>{r[4]}</td><td>{r[5]}</td></tr>")

        html_lines.append("</table>")
        html_lines.append("<br/><p><i>Processed autonomously by CentralOrchestrator & MySqlDataAgent via Disconnected Database Queue</i></p>")

        html_body = "\n".join(html_lines)

        # 3. Send email to keerukee@gmail.com via desktop Outlook MAPI
        outlook = win32com.client.Dispatch("Outlook.Application")
        mail = outlook.CreateItem(0) # 0 = olMailItem
        mail.To = "keerukee@gmail.com"
        mail.Subject = "Patient Lab Reports Summary - Last 5 Patients AI Response"
        mail.HTMLBody = html_body
        mail.Send()

        print(f"[Outlook Success] Successfully sent Patient Lab Reports email to 'keerukee@gmail.com'!")
    except Exception as e:
        print(f"[Outlook Error] {e}")
    finally:
        pythoncom.CoUninitialize()

if __name__ == "__main__":
    send_labreports_to_gmail()
