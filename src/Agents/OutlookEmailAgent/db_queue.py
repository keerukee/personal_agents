import pyodbc
import json
import uuid
from datetime import datetime, timezone

# Default SQL Server connection string for local instance
CONN_STR = (
    "DRIVER={ODBC Driver 17 for SQL Server};"
    "SERVER=localhost;"
    "DATABASE=AgentRegistryDb;"
    "Trusted_Connection=yes;"
    "TrustServerCertificate=yes;"
)

def get_connection():
    try:
        return pyodbc.connect(CONN_STR)
    except Exception as e:
        # Fallback for systems with ODBC Driver 18 or default driver
        fallback_str = (
            "DRIVER={SQL Server};"
            "SERVER=localhost;"
            "DATABASE=AgentRegistryDb;"
            "Trusted_Connection=yes;"
        )
        return pyodbc.connect(fallback_str)

def enqueue_inbound_event(source: str, prompt: str, data_json: dict) -> str:
    """Inserts a new event record directly into InboundEvents table in SQL Server."""
    event_guid = uuid.uuid4().hex
    now = datetime.now(timezone.utc).isoformat()
    json_str = json.dumps(data_json)
    
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute("""
        INSERT INTO InboundEvents (EventGuid, Source, Prompt, DataJson, Status, CreatedAt)
        VALUES (?, ?, ?, ?, 'Pending', ?)
    """, (event_guid, source, prompt, json_str, now))
    conn.commit()
    conn.close()
    return event_guid

def fetch_pending_tasks(agent_id: str):
    """Fetches pending tasks assigned to agent_id from AgentTasks table."""
    conn = get_connection()
    cursor = conn.cursor()
    cursor.execute("""
        SELECT TaskGuid, ParentEventGuid, Action, PayloadJson
        FROM AgentTasks
        WHERE TargetAgentId = ? AND Status = 'Pending'
        ORDER BY StepOrder, Id
    """, (agent_id,))
    rows = cursor.fetchall()
    conn.close()
    
    tasks = []
    for r in rows:
        tasks.append({
            "task_guid": r[0],
            "parent_event_guid": r[1],
            "action": r[2],
            "payload": json.loads(r[3]) if r[3] else {}
        })
    return tasks

def claim_task(task_guid: str) -> bool:
    """Claims a pending task by updating Status to InProgress."""
    conn = get_connection()
    cursor = conn.cursor()
    now = datetime.now(timezone.utc).isoformat()
    cursor.execute("""
        UPDATE AgentTasks
        SET Status = 'InProgress', StartedAt = ?
        WHERE TaskGuid = ? AND Status = 'Pending'
    """, (now, task_guid))
    rows_affected = cursor.rowcount
    conn.commit()
    conn.close()
    return rows_affected > 0

def complete_task(task_guid: str, result_json: dict, error_message: str = None):
    """Updates completed task with result or error in SQL Server."""
    conn = get_connection()
    cursor = conn.cursor()
    now = datetime.now(timezone.utc).isoformat()
    status = "Failed" if error_message else "Completed"
    result_str = json.dumps(result_json) if result_json else None
    
    cursor.execute("""
        UPDATE AgentTasks
        SET Status = ?, ResultJson = ?, ErrorMessage = ?, CompletedAt = ?
        WHERE TaskGuid = ?
    """, (status, result_str, error_message, now, task_guid))
    conn.commit()
    conn.close()

def fetch_event_results(parent_event_guid: str) -> str:
    """Fetches completed task result outputs for the parent event."""
    if not parent_event_guid:
        return ""
    try:
        conn = get_connection()
        cursor = conn.cursor()
        cursor.execute("""
            SELECT ResultJson
            FROM AgentTasks
            WHERE ParentEventGuid = ? AND Status = 'Completed' AND ResultJson IS NOT NULL
            ORDER BY StepOrder ASC
        """, (parent_event_guid,))
        rows = cursor.fetchall()
        conn.close()
        
        outputs = []
        for r in rows:
            try:
                res = json.loads(r[0])
                out = res.get("output") or res.get("resultOutput")
                if out:
                    outputs.append(str(out))
            except Exception:
                pass
                
        return "<br/><br/>".join(outputs)
    except Exception as e:
        print(f"[fetch_event_results Error] {e}")
        return ""
