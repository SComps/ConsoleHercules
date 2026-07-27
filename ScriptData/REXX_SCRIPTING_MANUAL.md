# HyperionTUI REXX Scripting Manual

This manual describes how to write and customize REXX automation scripts for **HyperionTUI**, the terminal interface and event automation engine for the **Hercules** mainframe emulator.

---

## 1. Overview & Architecture

HyperionTUI continuously monitors the Hercules system log. When log events or operator messages occur:
1. **Event Code Extraction**: The log engine scans incoming lines using structural pattern matching to extract the event code.
2. **REXX Master Execution**: The log line and extracted event code are passed to `MasterLogHandler.rex` in the `ScriptData/` directory.
3. **Command Output**: Any commands printed by the REXX script via `SAY` are sanitized and executed automatically against the Hercules console.

```
+------------------+     Log Line      +------------------------+
| Hercules Syslog  | ----------------> | SystemLogEventHandler  |
+------------------+                   +------------------------+
                                                   |
                                 ARG(1): EventCode | ARG(2): LogLine
                                                   v
+------------------+  Hercules Command +------------------------+
| Hercules Console | <---------------- | MasterLogHandler.rex   |
+------------------+    (via SAY)      +------------------------+
```

---

## 2. Universal Event Code Recognition

HyperionTUI uses a **generic structural regular expression** to detect event codes from Hercules, any guest operating system (MVS, z/OS, VM/370, z/VM, VSE), and any guest software application (DB2, CICS, IMS, VTAM, RACF, MQ):

$$\text{Pattern: } \text{\textbackslash b([A-Z]\{2,7\}\textbackslash d\{3,5\}[A-Z]\{0,2\}|\textbackslash \$[A-Z0-9]\{3,7\})\textbackslash b}$$

### Recognized Subsystem & Guest OS Message Formats

No component names or message prefixes are hardcoded. The engine automatically matches any message following standard mainframe conventions:

| Category / Software | Sample Log Message | Extracted `EventCode` |
| :--- | :--- | :--- |
| **Hercules Emulator** | `HHC01603I * HELLO FROM REXX` | `HHC01603I` |
| **DB2 on z/OS** | `DSNT408I SQLCODE = -805, ERROR: DBRM NOT FOUND` | `DSNT408I` |
| **DB2 Buffer Manager** | `DSNB207I DSNB1CIW BUFFER POOL BP0 HAS TOO FEW BUFFERS` | `DSNB207I` |
| **VM / CP (Control Program)** | `HCPMSG001I OPERATOR LOGON COMPLETE` | `HCPMSG001I` |
| **VM / CP Tape Mount** | `HCPMNT020E MOUNT TAPE ON 181` | `HCPMNT020E` |
| **VM / CMS** | `DMSITP143E COMMAND NOT FOUND` | `DMSITP143E` |
| **VM / RSCS** | `DMTNXS100I LINK ACTIVE TO NODE2` | `DMTNXS100I` |
| **MVS Job Scheduler** | `*02 IEF233A M 280,PRIVAT,SL,TAPEJOB,STEP1` | `IEF233A` |
| **MVS Data Management** | `*05 IEC141I 280,TAPE01,SL,MYJOB,STEP2` | `IEC141I` |
| **RMF / MF1 Reporter** | `1.39.12 STC 13 IRB101I MF/1 REPORT AVAILABLE FOR PRINTING` | `IRB101I` |
| **JES2 / JES3** | `$HASP373 JOB100 STARTED` | `$HASP373` |
| **CICS Transaction Server** | `DFHAC2001 TRANSACTION CSMI HAS TERMINATED` | `DFHAC2001` |
| **IMS Database** | `DFS0001I IMS INITIALIZATION COMPLETE` | `DFS0001I` |
| **WebSphere MQ** | `CSQY200I QUEUE MANAGER READY` | `CSQY200I` |
| **RACF / Security** | `ICH70001I LAST ACCESS AT 14:02:10` | `ICH70001I` |
| **VTAM Communications** | `IST314I END OF VTAM BUFFER LIST` | `IST314I` |
| **Custom Guest App** | `XYZ100E DATABASE RECOVERY REQUIRED` | `XYZ100E` |

If a log line does not contain a recognized structural code, `EventCode` is set to `"UNKNOWN"`, and the full raw text is still delivered in `LogLine`.

---

## 3. REXX Master Script Interface

All events are passed to `ScriptData/MasterLogHandler.rex`.

### Arguments Passed to REXX
- **`ARG(1)` (`EventCode`)**: The extracted event/notification ID string (e.g. `"DSNT408I"`, `"HCPMNT020E"`, `"IEF233A"`), or `"UNKNOWN"`.
- **`ARG(2)` (`LogLine`)**: The full raw log line as displayed in Hercules.

### Basic Script Structure
```rexx
/* REXX Master Log Event Handler */
PARSE ARG EventCode, LogLine

EventCode = STRIP(EventCode)
LogLine   = STRIP(LogLine)

/* Dispatch by Event Code or Prefix */
SELECT
  WHEN EventCode = 'IEF233A' | EventCode = 'IEC141I' THEN CALL HandleMvsTape Mount
  WHEN LEFT(EventCode, 3) = 'DSN' THEN CALL HandleDb2Message EventCode, LogLine
  WHEN LEFT(EventCode, 3) = 'HCP' THEN CALL HandleVmCpMessage EventCode, LogLine
  WHEN LEFT(EventCode, 3) = 'DMS' THEN CALL HandleVmCmsMessage EventCode, LogLine
  OTHERWISE
    /* Custom pattern match on raw text if EventCode is UNKNOWN or generic */
    IF POS('CRITICAL ERROR', LogLine) > 0 THEN DO
      SAY "message * ALERT: Critical error detected in guest OS"
    END
END

EXIT 0
```

---

## 4. Practical Automation Examples

### Example A: Automating DB2 on z/OS SQL & Buffer Pool Alerts
```rexx
HandleDb2Message: PROCEDURE
  PARSE ARG Code, MessageText

  /* Handle SQL Error notification */
  IF Code = 'DSNT408I' THEN DO
    PARSE VAR MessageText . 'SQLCODE =' SqlCode ',' .
    SAY "message * DB2 SQL Error " || STRIP(SqlCode) || " detected in guest"
  END

  /* Handle Buffer Pool Warning */
  IF Code = 'DSNB207I' THEN DO
    SAY "message * DB2 Buffer Pool shortage alert"
  END
RETURN
```

### Example B: Automating VM/370 and z/VM Operator Mounts
```rexx
HandleVmCpMessage: PROCEDURE
  PARSE ARG Code, MessageText

  /* VM CP Tape Mount Request: HCPMNT020E MOUNT TAPE ON 181 */
  IF Code = 'HCPMNT020E' THEN DO
    PARSE VAR MessageText . 'ON' DevAddr .
    DevAddr = STRIP(DevAddr)
    IF DevAddr <> "" THEN DO
      SAY "mount scratch.aws ON " || DevAddr
    END
  END
RETURN
```

### Example C: Responding to MVS WTOR Prompts (`reply` command)
```rexx
HandleMvsTape: PROCEDURE
  PARSE ARG MessageText

  ReplyId = ""
  IF POS('*', MessageText) = 1 THEN DO
    PARSE VAR MessageText '*' ReplyId .
    ReplyId = STRIP(ReplyId)
  END

  /* Auto-respond to operator prompt if Reply ID is present */
  IF ReplyId <> "" THEN DO
    SAY "reply " || ReplyId || ",U"
  END
RETURN
```

---

## 5. Security & Output Guardrails

To protect the host operating system, outputs generated by REXX scripts are checked by `RexxSecurityValidator`:

1. **Permitted Output Commands**:
   `MOUNT`, `UNMOUNT`, `DEVINIT`, `REPLY`, `IPL`, `START`, `STOP`, `LOGOPT`, `MESSAGE`, `MSGLOG`, `HERCULES`, `ATTACH`, `DETACH`.
2. **Shell Execution Block**:
   REXX system subcommands (`ADDRESS CMD`, `ADDRESS SYSTEM`, `ADDRESS BASH`, `ADDRESS SH`) are prohibited for security safety.
3. **Command Sanitization**:
   Output containing host shell injection characters (`;&|><\`$`) is suppressed before hitting Hercules.

---

## 6. Location, Configuration & Precedence

By default, scripts reside in the `ScriptData/` subdirectory relative to the application binary (`ScriptData/MasterLogHandler.rex`).

### Configuration Options

You can override the script path and allowed script directory using **environment variables** or **command line flags**:

| Configuration Target | Environment Variable | Command Line Flag | Default Path |
| :--- | :--- | :--- | :--- |
| **Master REXX Script** | `HYPERION_REXX_SCRIPT` | `--script <path>` or `-s <path>` | `ScriptData/MasterLogHandler.rex` |
| **Script Working Directory** | `HYPERION_SCRIPT_DIR` | `--script-dir <dir>` or `-d <dir>` | `ScriptData/` |

### Precedence Order

> [!IMPORTANT]
> **Command Line Arguments Always Override Environment Variables.**
> If both an environment variable and a command-line flag are supplied at launch, HyperionTUI will honor the command-line argument and ignore the environment variable value.

1. **Command Line Flags** (`--script`, `-s`, `--script-dir`, `-d`) — **Highest Precedence (Overrides Environment Variables & Defaults)**
2. **Environment Variables** (`HYPERION_REXX_SCRIPT`, `HYPERION_SCRIPT_DIR`) — **Secondary Precedence**
3. **Application Defaults** (`ScriptData/MasterLogHandler.rex`, `ScriptData/`) — **Fallback Default**

#### Command Line Example
```bash
HyperionTUI --script /etc/hyperion/custom_handler.rex --script-dir /etc/hyperion
```

#### Environment Variable Example (Linux / macOS)
```bash
export HYPERION_REXX_SCRIPT="/opt/hyperion/scripts/master.rex"
export HYPERION_SCRIPT_DIR="/opt/hyperion/scripts"
./HyperionTUI
```

#### Environment Variable Example (Windows PowerShell)
```powershell
$env:HYPERION_REXX_SCRIPT="C:\Hyperion\Scripts\master.rex"
$env:HYPERION_SCRIPT_DIR="C:\Hyperion\Scripts"
.\HyperionTUI.exe
```

---

## 7. Hot Reloading & Live Script Editing

HyperionTUI executes REXX scripts on demand via the REXX interpreter without caching compiled code in memory.

> [!TIP]
> **Live Editing / Hot Reloading Supported**  
> If you edit and save `MasterLogHandler.rex` (or any custom `.rex` script) while `HyperionTUI` is running, the **very next event** will immediately execute your updated script code from disk. No application restart or manual reload command is needed.
