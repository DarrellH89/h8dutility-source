;
; Code to load into the H89 with the bootstrp
; program. This provides the functions needed to
; save and restore disk images through the
; serial port.
;  On booting the H89, first use the B to load the jump tables
; into RAM, without any disk. Use Shift/Reset to get back
; to the H: prompt. Use the S command to enter the boot strap
; program. Use H89IMG.COM to load this program.
;  The command interpreter recognizes the
; following commands:
; W - Write image to disk. When each buffer is loaded
;      it replies with W. Each buffer must start with 'W'
;      or it will abort to command. Buffer size must match.
;      It expects the V value to match the disk in the drive.
;      Buffer data is transfered as 8 bit value.
; R - Reads a disk image, using  V value. Each buffer will
;     begin transfer when a 'R' is received. Buffer size
;     is the same as used for W command. A character, other
;     than r will abort command. Buffer data is transfered
;     as 8 bit values. Response handshake is 'r' if error.
; S - Save this image loader to disk as a stand alone boot
;      ( not HDOS ). The disk must be originally formatted
;      with V = 0. It returns a S when complete.
; V - Sets the volume number for the various operations.
;      Check HDOS docs for useage of the Volume number.
;      It must receive the volume number as a non-ascii
;      byte as the next byte after the the V command. It
;      returns a V when complete.
; C - Read the disk and returns the volume number if it is
;      an HDOS disk. If it is another type it would be an
;      indeterminate value.
; I - Fallowed by a number 0,1,2 corresponding to a sector
;      interleaving of 1:1, 1:2, or 1:3. Other numbers will
;      cause incorrect formatting during writes. This has
;      effect on the W and S commands only.
; T - Reads the value of the volume from the header of the
;      disk in the drive. It does this by looking at track
;      1's header and not by the HDOS image value.
; Other characters - It will reply with ? this is handy
;      to determine if it is truly in the command mode.

ZBUF    EQU     0A00H   ; Buffer size 1 track
ZSTK    EQU     100H     ; STACK SIZE

COMTYP  EQU     2313H  ; IF FAH THEN H8-5 ELSE H8-4/H89
                       ;  IN BOOT STRAP


; Ports for H8-4
LP4     EQU     0E0H
TX4     EQU     LP4
RX4     EQU     LP4
DVL4    EQU     LP4
DVH4    EQU     LP4+1
IER4    EQU     LP4+1
LCNTL4  EQU     LP4+3
MCNTL4  EQU     LP4+4
LSTAT4  EQU     LP4+5

; Ports for H8-5
LP5     EQU     0FAH
TX5     EQU     LP5
RX5     EQU     LP5
CNTL5   EQU     LP5+1
STAT5   EQU     LP5+1

; Disk addresses
DABORT EQU     2061H
DSDP   EQU     2085H
DSDT   EQU     2076H
DSTS   EQU     2088H
CLOCK   EQU     1C19H
WSP1    EQU     1EEDH
DWNB   EQU     2097H
DXOK   EQU     205EH
DWRITE EQU     206DH
DREAD  EQU     2067H
UIVEC  EQU     201FH
MFLAG  EQU     2008H
DTT    EQU     20A0H
DDLYHS EQU     20A4H
DDVCTL EQU     20A2H
DDLYMO EQU     20A3H
DDRVTB EQU     20A9H
DVOLPT EQU     20A7H
DWHDA  EQU     2053H
DDTS   EQU     2073H
DUDLY  EQU     208EH
DWSC   EQU     2091H
DRDB   EQU     2082H


        ORG 2329H       ; MATCHES WITH BOOTLDR
        NOP
        NOP
        NOP
        NOP
        DI
        LXI SP, SPINT
        LXI H, MAINPAD-1
        LXI B, BUFFER-MAININT+104H
        NOP
ALGNR1:
        NOP
        NOP
        NOP
        INX H
        MOV A, M
        ORA A
        JZ ALGNR1       ; FIND FIRST CODE
        LXI D, BUFFER-MAININT  ; COUNT TO MOVE
        DAD D   ; H POINTS TO END OF SHIFTED CODE
        LXI D, BUFFER   ; END OF CODE
        NOP
ALGNR2:
        NOP
        NOP
        NOP
        MOV A, M
        STAX D  ; MOVE IT UP
        DCX D
        DCX H
        DCR C
        JNZ ALGNR2      ; 256 BYTES?
        DCR B           ; CAUTION DOESN'T WORK RIGHT FOR
                        ;  SIZES OF EVEN 100H
        JNZ ALGNR2      ; ALL DONE?
        JMP MAININT
MAINPAD:
        NOP
        NOP
        NOP
        NOP
        NOP
        NOP
MAININT:
        DI
        MVI A, 0C3H     ; JMP
        STA UIVEC
        LXI H, CLOCK
        SHLD UIVEC+1
        LXI SP, SPINT
        CALL SINT
        MVI A, 01H
        STA MFLAG       ; TURN OFF COUNTER
        EI
        CALL DABORT ; TRACK 0
;        DI
        NOP
MAIN1:
        CALL CMND
        JMP MAIN1

SINT:
        LDA COMTYP
        CPI 0FAH  ; if H8-5 else H8-4 or H89
        JZ SINT5


SINT4:  ; For H8-4 and H89 with LP
        XRA A
        OUT LCNTL4      ; LINE CONTROL
        OUT IER4        ; NO INTERRUPTS
        OUT MCNTL4      ; INIT MODEM CONTROL
        DCR A           ; SHOULD BE 'MVI A, 80H' BUT 0FFH OK
        OUT LCNTL4
        MVI A, 0CH      ; 9600 BAUD
        OUT DVL4
        XRA A
        OUT DVH4
        MVI A, 07H      ; 8 BIT 2 STOPS
        OUT LCNTL4
        IN LSTAT4
        IN RX4           ; CLEAR ANY JUNK
        RET

SINT5:  ; For H8-5 serial
        MVI A, 0AAH
        OUT CNTL5
        MVI A, 040H
        OUT CNTL5       ; RESET 8251
        MVI A, 0CEH     ; ASYNC 2 STOP 8 BIT NO PARITY 16X
        OUT CNTL5
        MVI A, 015H     ; DON'T WANT INTRPTS
        OUT CNTL5       ; ENABLE TX/RX
        IN STAT5
        IN RX5           ; CLEAR ANY JUNK
        RET

CMND:
        CALL CHRIN
        CPI 0
        JZ CMND
        CPI 'R'
        JZ RDIMG
        CPI 'W'
        JZ WRIMG
        CPI 'S'
        JZ SVLDR
        CPI 'V'
        JZ SETV
        CPI 'C'
        JZ CHKV
        CPI 'I'
        JZ INTRLV
        CPI 'T'
        JZ RDDV
        MVI A, '?'
        JMP CHROUT

CHRIN:
        LDA COMTYP
        CPI 0FAH  ; if H8-5 else H8-4 or H89
        JZ CHRIN5

CHRIN4:
        IN LSTAT4
        RAR
        JNC CHRIN4        ; WAIT FOR CHAR
        IN RX4
        RET

CHROUT:
        MOV D, A
        LDA COMTYP
        CPI 0FAH  ; if H8-5 else H8-4 or H89
        JZ CHRO5
CHRO4:
        IN LSTAT4
        ANI 60H
        CPI 60H
        JNZ CHRO4
        MOV A, D
        OUT TX4
        RET

CHRIN5:
        IN STAT5
        RAR
        RAR
        JNC CHRIN5        ; WAIT FOR CHAR
        IN RX5
        RET

CHRO5:
        IN STAT5
        RAR
        JNC CHRO5
        MOV A, D
        OUT TX5
        RET

        ; FORMAT A SINGLE TRACK
        ; B = track C = vol#
FTRK:
        DI
        MVI A, 01
        STA MFLAG       ; TURN ON COUNTER
        MOV A, B
        STA DTT
        MVI A, 02
        STA DDLYHS
        XRA A
        OUT 7FH
        STA DDVCTL
        STA DDLYMO
        LXI H, DDRVTB+1
        SHLD DVOLPT
        MOV M, C
        EI
        CALL DSDP      ; SDP
        CALL DSDT      ; DIS INTRS
        XRA A
        OUT 7EH
        INR A
        STA DWHDA
        LDA DDVCTL
        INR A
        OUT 7FH
TRK1:
        CALL DSTS      ; SKIP THIS SECTOR
        LDA DDLYHS
        ANA A
        JNZ TRK1        ; WAIT DELAY
        LHLD DVOLPT
        MOV B, M        ; VOL#
        LHLD SECPNTR    ; SEC INTERLEAVE TABLE
TRK2:
        MVI C, 0AH
        CALL WSP1       ; WRITES 0'S
        MOV A, B        ; VOL#
        CALL DWNB
        LDA DTT        ; TRACK
        CALL DWNB
        MOV A, M        ; SEC#
        CALL DWNB
        INX H           ; INCR SEC PNTR
        MOV A, D        ; ?chksum?
        CALL DWNB
        MVI C, 10H
        CALL WSP1
TRK3:
        CALL DWNB
        DCR C           ; 256 0'S
        JNZ TRK3
TRK4:
        XRA A
        CALL DWNB      ; END PAD
        IN 7FH
        RAR
        JNC TRK4        ; UNTIL SEC END
        MOV A, M
        ORA A           ; 0 MARKS END OF SECTABLE
        JNZ TRK2        ; UNTIL END OF TRACK
        LDA DDVCTL
        OUT 7FH
        EI
        CALL DXOK
        MVI A, 14H
        STA DWHDA
        XRA A
        STA MFLAG       ; TURN OFF COUNTER ?
        RET

WRIMG:
        XRA A
        STA SECNUM
        STA CURTRK
        STA SECNUM+1
WRIMG1:
        LXI H, DDRVTB+1
        MOV M, A
        SHLD DVOLPT
        CALL CHRIN
        CPI 'W'         ; HANDSHAKE
        RNZ
        LXI H, BUFFER
        LXI B, ZBUF
WRIMG2:
        CALL CHRIN      ; GET DATA
        MOV M, A
        INX H
        DCX B
        MOV A, B
        ORA C
        JNZ WRIMG2
;
        LDA CURTRK
        MOV B, A
        ORA A
        JZ WRIMG3       ; C IS ZERO FROM ABOVE
        LDA VOLNUM      ;  ON FIRST TRACK
        MOV C, A        ;  USE VOL# ON THE REST
WRIMG3:
        CALL FTRK
        LDA CURTRK
        INR A
        STA CURTRK
;
        LXI B, ZBUF
        LXI D, BUFFER
        LHLD SECNUM
        CALL WRBUF
;
        MVI A, 'W'
        CALL CHROUT
        LHLD SECNUM
        LXI D, 0AH       ; SEC/TRK
        DAD D
        SHLD SECNUM
        LXI D, -190H     ; 400D IS MAX
        DAD D
        MOV A, H
        ORA L
        LDA VOLNUM
        JNZ WRIMG1       ; LAST TRACK?
        RET

WRBUF:
        ; BC = BUFFER SIZE
        ; DE = BUFFER ADDR
        ; HL = FIRST SEC#
        MVI A, 02
        STA DDLYHS
        CALL DWRITE
        RET

RDIMG:
        XRA A
        STA SECNUM
        STA SECNUM+1
RDIMG1:
        LXI H, DDRVTB+1
        MOV M, A
        SHLD DVOLPT
        CALL CHRIN
        CPI 'R'
        RNZ
;
        LXI B, ZBUF
        LXI D, BUFFER
        LHLD SECNUM
        CALL RDBUF
;
        LXI H, BUFFER
        LXI B, ZBUF
RDIMG2:
        MOV A, M
        CALL CHROUT
        INX H
        DCX B
        MOV A, B
        ORA C
        JNZ RDIMG2
;
        LDA GOODRD  ; LOOK FOR READ ERROR
        ORA A
        MVI A, 'R'
        JNZ RDIMG3
        ORI 020H    ; BAD READ SEND r
RDIMG3:
        CALL CHROUT
        LHLD SECNUM
        LXI D, 0AH       ; SEC/TRK
        DAD D
        SHLD SECNUM
        LXI D, -190H     ; 400D IS MAX
        DAD D
        MOV A, H
        ORA L
        LDA VOLNUM
        JNZ RDIMG1
        RET

RDBUF:
        ; BC = BUFFER SIZE
        ; DE = BUFFER ADDR
        ; HL = FIRST SEC#
        MVI A, 02
        STA DDLYHS
        CALL DREAD
        MVI A, 0
        JC RDBF1 ; IF CARRY, READ ERROR
        DCR A
RDBF1:  STA GOODRD
        RET

CHKV:
        XRA A
        STA SECNUM
        STA SECNUM+1
CHKV1:
        LXI H, DDRVTB+1
        MOV M, A
        SHLD DVOLPT
;
        LXI B, ZBUF
        LXI D, BUFFER
        LHLD SECNUM
        CALL RDBUF
;
        LDA BUFFER+900H
        CALL CHROUT
        MVI A, 'C'
        CALL CHROUT
        RET

SVLDR:
        XRA A
        STA VOLNUM
        LXI H, 2280H
        LXI D, DSKBOOT
        MVI C, DBEND-DSKBOOT
SVLDR1:
        LDAX D
        INX D
        MOV M, A
        INX H
        DCR C
        JNZ SVLDR1
;
        XRA A
        MOV B, A
        MOV C, A
        CALL FTRK
;
        XRA A
        LHLD DVOLPT
        MOV M, A
        LXI B, SECNUM-2280H
        LXI D, 2280H
        LXI H, 0
        CALL WRBUF
        MVI A, 'S'
        JMP CHROUT

RDDV:  ; READ DISK VOLUME
        LXI H, 20H  ; SOMEPLACE OFF TRACK 0
        PUSH H
        CALL DSDP
        POP H
        CALL DDTS
        MVI A,1
        CALL DUDLY
RDDV1:  CALL DSTS  ; SKIP SECTOR
        LDA DDLYHS
        ANA A
        JNZ RDDV1
        DI
        CALL DWSC
        CALL DRDB
        EI
        PUSH PSW
        CALL DABORT ; TRACK 0
        POP PSW
        CALL CHROUT
        MVI A, 'T'
        CALL CHROUT
        RET


DSKBOOT:
        JMP MAININT

SETV:
        CALL CHRIN
        STA VOLNUM
        MVI A, 'V'
        JMP CHROUT

INTRLV:
        CALL CHRIN
        ADD A
        MOV B, A
        ADD A
        ADD A
        ADD B   ; TIMES 10
        MOV C, A
        MVI B, 0
        LXI H, SEC1
        DAD B
        SHLD SECPNTR
        MVI A, 'I'
        JMP CHROUT

SEC1:   DB 0,1,2,3,4,5,6,7,8,9
SEC2:   DB 0,2,4,6,8,1,3,5,7,9
SEC3:   DB 0,3,6,9,2,5,8,1,4,7
SECEND: DB 0

GOODRD: DB 0
SECPNTR:        DW SEC1
VOLNUM: DB 0
CURTRK: DB 0
SECNUM: DW 0    ; 400D MAX 190H
DUMMY:  DB 0FFH, 0, 0FFH, 0     ; INSURE ASYNC ALIGNMENT

DBEND:
BUFFER:
        DS ZBUF

        DS ZSTK
SPINT:

 