import pythoncom
import win32com.client
import mysql.connector
import json
from datetime import datetime

def send_female_reports_to_gmail():
    pythoncom.CoInitialize()
    try:
        conn = mysql.connector.connect(host='localhost', port=3306, user='root', password='root')
        cursor = conn.cursor()

        # Query last 5 FEMALE lab reports from labreports & patients_info
        query = """
            SELECT 
                lr.PatientPID,
                p.FirstName,
                p.LastName,
                p.AgeYears,
                p.HospitalName,
                lr.ReportedAt,
                lr.Content,
                ai.Diagnostic,
                ai.PatientAdvice
            FROM labreports.lab_report lr
            INNER JOIN (
                SELECT DISTINCT PatientId, FirstName, LastName, AgeYears, HospitalName 
                FROM patients_info.vw_hospitalpatientcurrent
                WHERE SexAtBirth = 1 OR SexAtBirth = '1' OR SexAtBirth = 'Female' OR SexAtBirth = 'F'
            ) p ON lr.PatientPID = p.PatientId
            LEFT JOIN labreports.lab_report_ai ai ON lr.ReportId = ai.ReportId
            ORDER BY lr.CreatedAt DESC
            LIMIT 5;
        """

        cursor.execute(query)
        rows = cursor.fetchall()
        cursor.close()
        conn.close()

        html_lines = [
            "<h2>👩‍⚕️ REAL MySQL Lab Reports — Last 5 Female Patients Summary</h2>",
            "<p><b>Databases Joined:</b> <code>labreports.lab_report</code> ⟗ <code>patients_info.vw_hospitalpatientcurrent</code> (Filter: <code>SexAtBirth = Female</code>)</p>",
            f"<p><b>Query Time:</b> {datetime.utcnow().strftime('%Y-%m-%d %H:%M:%S')} UTC</p>",
            "<table border='1' cellpadding='8' cellspacing='0' style='border-collapse: collapse; font-family: Arial, sans-serif;'>",
            "<tr style='background-color: #D13438; color: white;'>",
            "<th>#</th><th>Female Patient Name</th><th>Age</th><th>Hospital</th><th>Reported Date</th><th>Key Lab Tests & Reference Ranges</th>",
            "</tr>"
        ]

        for idx, r in enumerate(rows, 1):
            pid, fname, lname, age, hospital, reported_at, content_json, diagnostic, advice = r
            full_name = f"{fname} {lname}".strip() if fname else f"Patient PID: {pid[:8]}..."
            age_str = str(age) if age else "N/A"
            hosp_str = str(hospital) if hospital else "Hospital"
            date_str = reported_at.strftime('%Y-%m-%d') if reported_at else "N/A"

            test_list = []
            try:
                content = json.loads(content_json)
                reports = content.get("Reports", {})
                for category, tests in reports.items():
                    if isinstance(tests, dict):
                        for test_name, test_data in tests.items():
                            if isinstance(test_data, dict):
                                val = test_data.get("Report")
                                std_val = test_data.get("StandardValue")
                                is_abnormal = test_data.get("IsAbnormal", False)
                                if val and str(val).strip():
                                    ref_str = f" <span style='color: #555; font-style: italic;'>(Ref: {str(std_val).strip()})</span>" if std_val and str(std_val).strip() else ""
                                    mark = " <span style='color: red; font-weight: bold;'>⚠️ (ABNORMAL)</span>" if is_abnormal else ""
                                    test_list.append(f"<b>{test_name}:</b> {str(val).strip()}{ref_str}{mark}")
            except Exception:
                pass

            test_summary = "<br/>".join(test_list[:5]) if test_list else "Lab record processed"

            html_lines.append(f"<tr><td>{idx}</td><td><b>{full_name}</b></td><td>{age_str}</td><td>{hosp_str}</td><td>{date_str}</td><td>{test_summary}</td></tr>")

        html_lines.append("</table>")
        html_lines.append("<br/><p><i>Extracted autonomously from real MySQL database schemas by CentralOrchestrator & MySqlDataAgent</i></p>")

        html_body = "\n".join(html_lines)

        outlook = win32com.client.Dispatch("Outlook.Application")
        mail = outlook.CreateItem(0) # 0 = olMailItem
        mail.To = "keerukee@gmail.com"
        mail.Subject = "Last 5 Female Patient Lab Reports - Real MySQL Query Result"
        mail.HTMLBody = html_body
        mail.Send()

        print(f"[Outlook Success] Successfully sent Last 5 Female Patient Lab Reports to 'keerukee@gmail.com'!")
    except Exception as e:
        print(f"[Outlook Error] {e}")
    finally:
        pythoncom.CoUninitialize()

if __name__ == "__main__":
    send_female_reports_to_gmail()
