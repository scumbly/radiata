REQUIREMENTS
------------
- Windows 10 or 11, x64 (Intel/AMD). Windows on ARM isn't supported.
- A controller.

WHAT GETS INSTALLED
-------------------
Radiata.exe (one self-contained file), a "drivers" folder, and a small helper for the
built-in Arcade, installed under your user profile (%LOCALAPPDATA%\Programs\Radiata) with
a Start menu entry. The app launches the driver installers from that folder.
On a first install Radiata is also set to start with Windows (turn that off from the tray
menu or Settings > Advanced), and the driver step registers a sign-in recovery task that
makes sure your controller isn't left hidden.

INSTALL (about 3 minutes)
-------------------------
1. Run the Radiata-{VERSION}-setup.exe you downloaded.

2. SMARTSCREEN: the installer is code-signed, but Windows can still show a blue "Windows
   protected your PC" box for a new or rarely downloaded file until its publisher has built
   up reputation. If you see it, and the file came from getradiata.app or the Radiata
   releases page on GitHub, click "More info", then "Run anyway".

3. In setup, leave "Install drivers (recommended)" ticked.
   Radiata installs to your user profile, so the install itself needs no admin rights;
   the only Windows security (UAC) prompts come from the driver step at the end, which installs
   two small open-source drivers:
     - ViGEmBus lets Radiata present a virtual gamepad
     - HidHide hides your real pad from the game so inputs don't double up

   APPROVE the UAC prompts and each driver's installer. During this step Radiata also registers
   itself with HidHide so it can still see the controller it hides from other apps. (These
   drivers are signed by their developer; they're the standard tools DS4Windows/reWASD use.)

4. Finish setup with "Launch Radiata" ticked. FIRST-RUN SETUP opens automatically and
   walks you through everything: connect your controller, try the wheel, pick a look.
   When you finish, Radiata lives in the SYSTEM TRAY (bottom-right of the taskbar,
   the little flower). Click it for Settings.

UNINSTALL
---------
- Uninstall from Windows Settings > Apps > Radiata, or in the app under Settings >
  Advanced > Troubleshooting > Uninstall Radiata... Either removes the program and
  Radiata's Windows changes (startup entry, controller cloak, sign-in recovery task).
  The uninstall dialog has two options, both OFF by default: also delete your Radiata
  settings and cover-art cache, and also uninstall the ViGEmBus/HidHide drivers (other
  controller tools use them). Approve the one admin prompt. If driver removal reports
  they're in use, restart Windows and remove them via Windows Settings > Apps.
- Your settings live in %APPDATA%\Radiata. Backups you make default to
  Documents\Radiata Backups.

FOUND A BUG?
------------
Easiest: Settings > Advanced > Troubleshooting > "Email Log to Developer..." It saves a
zip of the diagnostic log to your Desktop and opens a pre-addressed email; attach the zip
and describe what happened. Open the zip and look through it before you send it: the log
can include device identifiers, account names in file paths, and application or game
names. Radiata never sends it for you.
You can also open an issue at https://github.com/scumbly/radiata/issues. That page is
public, so attach the zip there only after you've reviewed it.

