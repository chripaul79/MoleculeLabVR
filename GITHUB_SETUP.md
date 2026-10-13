# Upload with GitHub Desktop on Mac

1. Open GitHub Desktop and choose **File → Add Local Repository**.
2. Select this `MoleculeLab-GitHub` folder.
3. Review the files, enter `Initial Molecule Lab project` as the summary and click **Commit to main**.
4. Click **Publish repository**, choose the repository name and visibility, then publish.

No GitHub remote is configured and nothing has been uploaded yet. Existing asset licences are retained; no new blanket licence has been assigned.

## Open in Unity
Add this folder in Unity Hub and use **6000.6.2f1**. Open `Assets/Scenes/MoleculeLab.unity`. Unity recreates Library and other caches on first import. Assets, their `.meta` files, package manifests and project settings are included.

Builds, APKs, backups, generated IDE files and personal Unity settings are excluded. Current files are below GitHub's per-file size limit, so Git LFS is not required for this initial copy. Keep large future assets in Git LFS and distribute APKs as release attachments.

This is a separate copy. Make future changes in this folder if you want GitHub Desktop to track them; edits in the original project do not sync automatically.
