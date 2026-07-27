/* REXX Master Log Event Handler for HyperionTUI / Hercules */
/* ========================================================================= */
/* Input Arguments:                                                          */
/*   ARG(1) - EventCode  (e.g., "IEF233A", "IEC141I", "HHC02279I", "UNKNOWN") */
/*   ARG(2) - LogLine    (Full text of the log entry)                        */
/*                                                                           */
/* Output:                                                                   */
/*   SAY statements output Hercules commands to be executed.                 */
/* ========================================================================= */

PARSE ARG EventCode, LogLine

EventCode = STRIP(EventCode)
LogLine   = STRIP(LogLine)

/* Example Tape Request Message Formats:                                    */
/*   MVS IEF233A: "*02 IEF233A M 280,PRIVAT,SL,TAPEJOB,STEP1"              */
/*   MVS IEC141I: "*05 IEC141I 280,TAPE01,SL,MYJOB,STEP2"                  */

IF EventCode = 'IEF233A' | EventCode = 'IEC141I' THEN DO
  CALL HandleTapeRequest LogLine
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
