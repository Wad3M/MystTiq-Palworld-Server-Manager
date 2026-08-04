# GitHub Setup Walkthrough — Part 3 Package

## 1. Extract this ZIP

Extract it to a permanent folder, for example:

```text
C:\Projects\MystTiq-Palworld-Server-Manager
```

## 2. Replace the GitHub username placeholders

Search the entire project for:

```text
Wad3M
```

Replace it with your GitHub username. The placeholders currently appear in the project metadata, README/site links, and GitHub Pages site.

## 3. Review before publishing

Read `PRE_PUBLICATION_REVIEW.md`. Do not add real saves, backups, logs, server passwords, REST passwords, Steam credentials, or tokens.

## 4. Add the local repository in GitHub Desktop

1. Open GitHub Desktop.
2. Choose **File → Add local repository**.
3. Select the extracted folder.
4. When told it is not a Git repository, choose **create a repository**.
5. Name: `MystTiq-Palworld-Server-Manager`.
6. Git ignore: **None**.
7. License: **None**.
8. Create the repository.

The package already contains `.gitignore` and the MIT `LICENSE`.

## 5. First commit

Commit summary:

```text
Initial public release v0.2.12
```

Then click **Commit to main**. Do not publish until the changed-file list has been checked for personal data.

## 6. Publish

Click **Publish repository**, uncheck **Keep this code private**, and publish.

## 7. Enable GitHub Pages

On GitHub: **Settings → Pages → Source: GitHub Actions**. The included workflow publishes the `docs` folder.

## 8. Build release files locally

Open PowerShell in the repository folder:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
./scripts/Package-Portable.ps1
```

For the installer, install Inno Setup 6 and run:

```powershell
./scripts/Build-Installer.ps1
```

## 9. Automated releases

After the repository is online, pushing a tag such as `v0.2.12` starts the included Release workflow and creates the portable ZIP automatically.
