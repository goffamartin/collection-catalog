---
name: verify-in-browser
description: Verifying changes in the running Collection Catalog app – building next to a running instance, an own instance over a copy of the data (or demo data), a test user, sign-in and walkthroughs with Playwright. Use after every UI/navigation/migration change before saying it is done.
---

# Verify in the browser

A developer's own instance (`dotnet run`) **locks `bin/`** and **shares `src/CollectionCatalog.Web/App_Data`**.
Never kill it and never run a newer version over its data (a migration would break the older instance).

## 1. Build outside the locked bin
```bash
A="<temp>/artifacts"
dotnet build --artifacts-path "$A" 2>&1 | grep -E " error |warning CS|warning RZ|Build succeeded" | sort -u
dotnet test  --artifacts-path "$A" --no-build 2>&1 | grep -E "Passed!|Failed!|  Failed"
```
- Tailwind CSS is generated during the build – without `node_modules` run `npm ci` first.

## 2. Data
- A copy of real data: `cp -r src/CollectionCatalog.Web/App_Data "<temp>/data-copy"`, or
- demo data into an empty folder: `Storage__DataPath="<temp>/demo" dotnet "$A/bin/CollectionCatalog.Web/debug/CollectionCatalog.Web.dll" seed-demo`.

## 3. Test user (never print the password into a chat or log)
```bash
python -c "import secrets;print('test-'+secrets.token_urlsafe(12))" > "<temp>/test-password.txt"
Storage__DataPath="<temp>/demo" dotnet "$A/bin/CollectionCatalog.Web/debug/CollectionCatalog.Web.dll" \
  user-set-password --username tester < "<temp>/test-password.txt"
```
- **The command must be the first argument** – otherwise the web server starts instead.
- Redirecting stdin from a file works; on Git Bash `< /dev/null` is not detected as redirected input.

## 4. Own instance (in the background)
```bash
Storage__DataPath="<temp>/demo" ASPNETCORE_ENVIRONMENT=Development \
  dotnet "$A/bin/CollectionCatalog.Web/debug/CollectionCatalog.Web.dll" --contentRoot src/CollectionCatalog.Web --urls http://localhost:5296
curl -s --retry 15 --retry-all-errors --retry-delay 1 -o /dev/null -w 'health %{http_code}\n' http://localhost:5296/health
```
Stop the instance afterwards – it locks `$A` for the next build.

## 5. Playwright
- Keep a walkthrough in one script; wait ~1–1.5 s after load for the interactive circuit to connect.
- Sign-in: `input[name="Input.UserName"]`, `input[name="Input.Password"]`, `button[type=submit]`. In the admin the
  first submit button in the DOM may be *Log out* – narrow selectors (`main button:has-text("Save")`).
- Mobile: viewport 390×844 with touch (activates `pointer-coarse:`); check both languages (`/` and `/cs/`).
- Collect console errors; when something hangs, look for `fail:` / `Unhandled exception` in the server log.
- Delete test records afterwards.
