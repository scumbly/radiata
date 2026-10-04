# Security policy

## Reporting a vulnerability

Please report security problems **privately**, not in a public issue or pull request.

- Email **contact@jesseholden.com** with "Radiata security" in the subject, or
- use GitHub's private vulnerability reporting: the **Report a vulnerability** button under this
  repository's **Security** tab (available when the repository has it enabled).

Include the Radiata version (**Settings ▸ About**), your Windows version, what you found, the steps to
reproduce it, and the impact as you understand it. A proof of concept helps; please don't test against
anyone else's machine or data.

## What is in scope

- The Radiata application (`Radiata.exe` and its Arcade helper process, `Radiata.ArcadeHost.exe`).
- The installer and uninstaller (`packaging/radiata.iss`) and the elevated steps they run.
- The update feed at `getradiata.app/update` and the crash-report endpoint beside it.
- The release pipeline (`.github/workflows/release.yml`) and the integrity of published installers.

## What is out of scope

- **The bundled drivers.** ViGEmBus and HidHide are separate projects by Nefarius; report driver
  vulnerabilities upstream at [ViGEmBus](https://github.com/nefarius/ViGEmBus) or
  [HidHide](https://github.com/nefarius/HidHide). If a flaw in how Radiata uses a driver makes it
  exploitable, that part is in scope.
- Third-party libraries and services (report them to their own projects, and tell me if Radiata's use of
  one is what makes it exploitable).
- Attacks that require an already compromised machine or administrator account, social engineering, and
  denial of service by a user against their own PC.

## What to expect

Radiata is maintained by one person. I will acknowledge a report as soon as I can, keep you informed as I
investigate, and credit you in the fix's release notes unless you would rather stay anonymous. I can't
promise fixed response or fix times, and I ask that you give me a reasonable chance to ship a fix before
you disclose a problem publicly.

Security fixes are made in the latest release; older versions are not patched. Radiata is pre-1.0, and the
in-app updater is how a fix reaches installed copies.

## Verifying a download

Download Radiata only from [GitHub Releases](https://github.com/scumbly/radiata/releases) or
[getradiata.app](https://getradiata.app). The installer is code-signed (Azure Artifact Signing, with a
trusted timestamp). To check one:

- In File Explorer, right-click the installer, choose **Properties**, and look for a **Digital
  Signatures** tab whose signature reports as valid; or
- run the repository's check from a PowerShell prompt, which fails unless the signature is valid and
  timestamped:

  ```
  .\tools\verify-signature.ps1 .\Radiata-<version>-setup.exe
  ```

- Compare the file's hash with the `sha256` in the `latest.json` attached to the same release:

  ```
  Get-FileHash .\Radiata-<version>-setup.exe -Algorithm SHA256
  ```

The in-app updater downloads over HTTPS only, accepts only installers hosted under this repository's
release downloads, checks the SHA-256 from the update feed, and discards any file that doesn't match.
