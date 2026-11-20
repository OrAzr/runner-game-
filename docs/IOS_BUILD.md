# iOS TestFlight build

This installs the Unity game on an iPhone through TestFlight. GitHub Actions builds it. You merge the pull request yourself, then add the secrets below and run the workflow.

The workflow file is `.github/workflows/ios-testflight.yml`. It does not change the browser Pages deploy.

- **Unity export** runs on Linux with [GameCI](https://game.ci/docs/github/builder) and Unity **2022.3.68f1** (the version in `ProjectSettings/ProjectVersion.txt`, which has a GameCI iOS image).
- **Signing and upload** run on a GitHub macOS runner. [fastlane match](https://docs.fastlane.tools/actions/match/) creates the Apple Distribution certificate and the App Store provisioning profile during the job and stores them, encrypted, in a private git repo you create. You do not need a Mac for signing.

The iOS bundle id is `com.outpostzero.game`. The app is portrait. The marketing version is `0.2.0`. The TestFlight build number is the GitHub Actions run number.

## Secrets to add

On your phone, open the repo in a **browser** (the GitHub app hides this page):

`https://github.com/OrAzr/runner-game-/settings/secrets/actions`

New repository secret for each row. Paste the value only. Never commit these files.

| Secret | What to paste |
| --- | --- |
| `UNITY_EMAIL` | Email of the Unity account that owns the license. |
| `UNITY_PASSWORD` | Password for that Unity account. |
| `UNITY_LICENSE` | Full text of the Personal license file `Unity_lic.ulf`. Leave this unset if you use a Pro/Plus serial instead. |
| `UNITY_SERIAL` | Unity Pro/Plus serial (`XX-XXXX-XXXX-XXXX-XXXX-XXXX`). Leave this unset if you set `UNITY_LICENSE`. |
| `APP_STORE_CONNECT_KEY_ID` | The Key ID of the App Store Connect API key (10 characters). |
| `APP_STORE_CONNECT_ISSUER_ID` | The Issuer ID shown above the keys list on App Store Connect. |
| `APP_STORE_CONNECT_KEY_BASE64` | Base64 of the downloaded `AuthKey_XXXX.p8` file, one line, no quotes. |
| `APPLE_TEAM_ID` | Your 10-character Apple Developer Team ID. |
| `MATCH_GIT_URL` | HTTPS URL of the private certificates repo, for example `https://github.com/OrAzr/runner-game-ios-certs.git`. |
| `MATCH_GIT_TOKEN` | A GitHub fine-grained token that can read and write that private repo only. |
| `MATCH_PASSWORD` | A long passphrase you invent. fastlane uses it to encrypt the certificates in that repo. Save it in a password manager. You cannot recover the certs without it. |

Set **either** `UNITY_LICENSE` **or** `UNITY_SERIAL`, not both. The workflow stops at the first step and lists any name that is missing.

## Do these in order

### 1. Unity account

**Phone browser.** If you do not already have one, create a free account at [id.unity.com](https://id.unity.com/). Use the same email you will store as `UNITY_EMAIL`.

A Personal license is enough. A Pro/Plus subscription also works and skips the `.ulf` file in the next step.

### 2. Activate the license and copy it

**Needs a computer (Windows, Mac, or Linux). A phone cannot do this.** It does not have to be a Mac. GameCI's current Personal-license steps are at [game.ci/docs/github/activation](https://game.ci/docs/github/activation).

1. Install [Unity Hub](https://unity.com/download) on that computer. You do not need to install the editor.
2. Sign in with the Unity account from step 1.
3. Unity Hub → Preferences → Licenses → **Add** → **Get a free personal license**. Click through until the license is added. Hub only writes the `.ulf` file if you use Add. Seeing a license in the list is not enough.
4. Copy the whole file:
   - Windows: `C:\ProgramData\Unity\Unity_lic.ulf`
   - Mac: `/Library/Application Support/Unity/Unity_lic.ulf`
   - Linux: `~/.local/share/unity3d/Unity/Unity_lic.ulf`
5. Paste the entire file, including the first and last lines, into the `UNITY_LICENSE` secret.
6. Sign out of Unity Hub on that computer. Hub 3.7 and later has no "Return license" button for Personal seats. Signing out releases the seat so CI can use it. If CI later says the seat is already in use, sign out on every machine where that Unity account is logged into Hub.

If you have Unity Pro or Plus instead, skip the `.ulf` file. Put the serial in `UNITY_SERIAL`. In Hub, return any Professional seat you activated locally before the first CI run so you do not hold two seats.

Also add `UNITY_EMAIL` and `UNITY_PASSWORD`.

### 3. App Store Connect API key

**Phone browser.** You already have an Apple Developer account.

1. Open [App Store Connect → Users and Access → Integrations → App Store Connect API](https://appstoreconnect.apple.com/access/integrations/api) (the page may be under Users and Access → Keys).
2. If asked, request access to the API. Accept the terms.
3. Generate an API key with access **Admin**. App Manager is not enough to create a certificate.
4. Download `AuthKey_XXXXXXXXXX.p8`. Apple lets you download it **once**. Keep the file.
5. Copy the **Key ID** into `APP_STORE_CONNECT_KEY_ID`.
6. Copy the **Issuer ID** (above the keys table) into `APP_STORE_CONNECT_ISSUER_ID`.

Base64-encode the `.p8` file into `APP_STORE_CONNECT_KEY_BASE64`:

- **On a computer:** `base64 -i AuthKey_XXXXXXXXXX.p8 | tr -d '\n'` (Mac) or `base64 -w 0 AuthKey_XXXXXXXXXX.p8` (Linux). Copy the one line of output.
- **On an iPhone:** open the Shortcuts app → new shortcut → Add "Encode" → Base64, input the `.p8` from Files. Paste the result into the secret with no line breaks and no quotes. Do not encode a screenshot or the filename.

The decoded text must start with `-----BEGIN PRIVATE KEY-----`. The workflow checks that and stops if it does not.

### 4. Team ID

**Phone browser.** Open [developer.apple.com/account](https://developer.apple.com/account) → Membership. Copy the 10-character **Team ID** into `APPLE_TEAM_ID`.

### 5. Private repo for the certificates

**Phone browser.** This is the only extra GitHub repo. It stays private. The game repo is public, so the certificates must not live there.

1. GitHub → New repository. Name it `runner-game-ios-certs`. Set it to **Private**. Check **Add a README** so the repo is not empty. Leave the default branch named `main` (the workflow pushes certificates to `main`). Create it.
2. Copy the HTTPS URL, ending in `.git`, into `MATCH_GIT_URL`. Example: `https://github.com/OrAzr/runner-game-ios-certs.git`.
3. GitHub → Settings → Developer settings → Personal access tokens → **Fine-grained tokens** → Generate.
   - Resource owner: your user.
   - Repository access: **only** `runner-game-ios-certs`.
   - Permissions: Contents **Read and write**.
4. Paste the token into `MATCH_GIT_TOKEN`.
5. Invent a long passphrase and store it in `MATCH_PASSWORD` and in your password manager.

You do **not** create the Apple certificate yourself. The first workflow run creates an Apple Distribution certificate and an App Store profile with the API key, then pushes them to this private repo. Later runs reuse them. Do not also create a distribution certificate in the Apple developer portal, or you can hit Apple's limit of three distribution certificates.

### 6. Add every secret, then merge

**Phone browser.** Add the secrets in the table above. Then merge the pull request that added this workflow. Merging does not by itself start an iOS build unless the merge also changed files under `unity/`.

### 7. Run the workflow

**Phone browser.** Open the repo → Actions → **iOS TestFlight** → Run workflow → Run workflow. The branch is `main`.

It also runs on its own when a push to `main` changes `unity/**` or this workflow file.

The Linux job is the long one (Unity import and IL2CPP). When the macOS job is green, the IPA is uploaded. Apple still processes the build for several minutes before it appears in TestFlight. The workflow does not wait for that processing.

If App Store Connect says the name **Outpost Zero** is already taken, edit `APP_NAME` in `unity/OutpostZero/fastlane/Fastfile` to a unique name and run the workflow again. The bundle id stays `com.outpostzero.game`.

### 8. Install on the iPhone

**iPhone.** Install Apple's TestFlight app. The Apple ID on the phone must be a user on the App Store Connect team (your developer account already is). Open TestFlight when the build finishes processing and install Outpost Zero.

Internal testers do not need a beta review. The first build can take a few extra minutes to show up.

## What the workflow does

1. Checks that every secret above is set. If one is missing, the log names it and stops before Unity starts.
2. Restores a cache of `unity/OutpostZero/Library` so later runs skip a cold import.
3. Runs `OutpostZero.EditorTools.IosBuild.Build` inside Unity 2022.3.68f1. That builds `Assets/Scenes/Main.unity` from `OutpostSceneBuilder`, sets the bundle id, portrait orientation, and the run number, then writes an Xcode project.
4. On macOS, fastlane creates the App Store Connect app record if needed, creates or downloads the signing certificate, builds the IPA, and uploads it.

## If a run fails

- **Missing secrets:** the first step lists the names. Add them and re-run.
- **Unity seat already in use:** sign out of Unity Hub on the computer from step 2, then re-run.
- **`.p8` did not decode:** re-encode the key file itself, one line, no quotes.
- **Name already taken:** change `APP_NAME` in the Fastfile.
- **Certificate limit:** revoke an unused Apple Distribution certificate in the developer portal, or keep using the one match already stored. Do not create a second certificate by hand.
- **TestFlight build missing:** wait for processing. The green workflow only means the upload was accepted.
