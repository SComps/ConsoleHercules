/* REXX Master Log Event Handler for HyperionTUI / Hercules */
/* ========================================================================= */
/* Input Arguments:                                                          */
/*   ARG(1) - EventCode  (e.g., "IEF233A", "HCPMSG001I", "DMSITP143E", "HHC02279I") */
/*   ARG(2) - LogLine    (Full text of the log entry)                        */
/*                                                                           */
/* Output:                                                                   */
/*   SAY statements output Hercules commands to be executed.                 */
/* ========================================================================= */

PARSE ARG EventCode  LogLine

EventCode = STRIP(EventCode)
LogLine   = STRIP(LogLine)

/* Ignore Hercules informational message echoes to prevent infinite recursion */
IF EventCode = 'HHC01603I' THEN EXIT 0

/* MF/1 Report Availability Message */
IF EventCode = 'IRB101I' THEN DO
  SAY "/P MF1"
  EXIT 0
END

/* MVS Tape Request Messages */
IF EventCode = 'IEF233A' | EventCode = 'IEC141I' THEN DO
  CALL HandleTapeRequest LogLine
  EXIT 0
END

/* VM/CP, CMS, and RSCS Messages */
IF LEFT(EventCode, 3) = 'HCP' | LEFT(EventCode, 3) = 'DMS' | LEFT(EventCode, 3) = 'DMT' THEN DO
  CALL HandleVmMessage EventCode, LogLine
  EXIT 0
END

EXIT 0

/* ========================================================================= */
/* Sample Subroutine: HandleTapeRequest                                      */
/* Extracts Reply ID, Device Address, and VOLID from tape mount prompts      */
/* ========================================================================= */
HandleTapeRequest: PROCEDURE
  PARSE ARG LineToParse

  ReplyId  = ""
  DevAddr  = ""
  VolId    = ""

  /* Extract WTOR Reply ID if line starts with '*' */
  IF POS('*', LineToParse) = 1 THEN DO
    PARSE VAR LineToParse '*' ReplyId RestOfLine
    ReplyId = STRIP(ReplyId)
  END
  ELSE DO
    RestOfLine = LineToParse
  END

  /* Parse message fields separated by commas or spaces */
  /* Line sample: "*02 IEF233A M 280,PRIVAT,SL,TAPEJOB,STEP1" */
  PARSE VAR RestOfLine MsgCode SubAction RestParams

  /* Extract DevAddr and VolId from parameters "280,PRIVAT,SL..." */
  IF POS(',', RestParams) > 0 THEN DO
    PARSE VAR RestParams DevAddr ',' VolId ',' .
  END
  ELSE DO
    PARSE VAR RestParams DevAddr VolId .
  END

  DevAddr = STRIP(DevAddr)
  VolId   = STRIP(VolId)

  /* Example Actions user can customize: */
  IF VolId <> "" AND DevAddr <> "" THEN DO
    /* 1. Mount the requested tape image onto the Hercules tape drive */
    SAY "mount " || VolId || ".aws ON " || DevAddr

    /* 2. Respond to OS WTOR operator prompt if a Reply ID was present */
    IF ReplyId <> "" THEN DO
      SAY "reply " || ReplyId || ",U"
    END
  END

RETURN

/* ========================================================================= */
/* Sample Subroutine: HandleVmMessage                                        */
/* Handles VM/CP (HCP...), CMS (DMS...), and RSCS (DMT...) log events         */
/* ========================================================================= */
HandleVmMessage: PROCEDURE
  PARSE ARG Code, LineToParse

  /* Example: Handle VM CP tape mount prompt HCPMNT020E MOUNT TAPE ON 181 */
  IF Code = 'HCPMNT020E' THEN DO
    PARSE VAR LineToParse . 'ON' DevAddr .
    DevAddr = STRIP(DevAddr)
    IF DevAddr <> "" THEN DO
      /* Auto-mount tape if configured */
      /* SAY "mount scratch.aws ON " || DevAddr */
    END
  END

RETURN
