# MVP capture and extraction audit

Audit of the checkout on 27 November 2026. Source and tests were not intentionally edited. PASS below describes the specific inspected or executed evidence, not an end-to-end certification. UNVERIFIED means the requested evidence was not obtained. Tests cited by name were not necessarily run individually.

## 1. Command output and scope

### Step 0: state

`git status` in both the root and `web`:

```text
On branch main
Your branch is up to date with 'origin/main'.

nothing to commit, working tree clean
```

`git log --oneline -25`, root:

```text
4402ef4 Update subproject reference to latest commit
15b7121 Add comprehensive tests for receipt extraction and recovery processes
221dddc Refactor code structure for improved readability and maintainability
16a7f89 Fix image API review findings
142e8e1 Add PostgreSQL receipt image storage and API
1dc8006 Update README.md to clarify client type and enhance extraction details
23ac797 Update web submodule with reusable receipt capture
1005a71 Step C: add extraction schema constraints
7e834b5 Step B: compose receipt field parser
7712d2b Step 3: refactor extraction pipeline and add document classification features
dd7cdb6 Step 3: add vendor extractor
0baa65f Step 3: add tax invoice detector
a075a0c Step 3: add VAT classifier
3b17878 Step 3: add VAT number validator
30de5b0 Step 3: add printed VAT amount extractor
4618c3d Step 3: add date parser
ada8376 Step 3: add amount parser
a776c44 feat: Add expense extraction tables and models
68b1a81 feat(auth): update email verification to require password and add rate limiting
7bac1b4 Mark subproject as dirty due to uncommitted changes in the web directory.
27e0be1 chore: Mark web submodule as dirty
95427dc chore: Update web submodule to latest commit
83ea54c Add Product Requirements Document and Analysis Summary
f8a4a39 feat: Add web submodule for biz-zen-guard integration
b90138c login and registration
```

`git log --oneline -25`, web:

```text
884fdd0 feat: add UploadedImageGallery and ExtractionReview components
a8b3ff7 Add mobile development script to package.json
640ac18 Add HTTPS setup script and configuration for local development
24e923c Verify and optimise receipt capture for the mobile PWA
4f0a7d5 Replace simulated receipt capture with camera and draft review
c6d97d2 Remove references to data storage location in various components for improved privacy compliance
5fceee8 Remove references to data storage location in various components for improved privacy compliance
cb11aa7 Enhance ExpensesPage with detailed expense view and improved UI components
4f9311a Add invoice images and update gallery receipt image logic
6224f8d Add allowedHosts configuration for Vercel preview deployment
685350c Refactor code structure for improved readability and consistency
8ad67a4 Add project README
d0ea9b6 Added real export page
a31fdc3 Changes
841c889 Changes
ce5ec45 Changes
25fc454 Changes
62ced8d Changes
7941e46 Added realistic receipt images
574676d Changes
1537a33 Changes
a1af585 Enabled mock mode toggle
1dcf8b7 Changes
24f4616 Changes
7d59b76 Changes
```

`git submodule status`: `884fdd0007b3b934e63d4f8006aa7cc18df0c0dc web (heads/main)`. `dotnet --version`: `10.0.300`; initial `node --version`: `v18.17.0`; browser checks used the installed `v22.14.0`. `df -h .` reported a 926 GiB filesystem, 586 GiB used and 283 GiB available; percentage columns are omitted to comply with the requested report format. Host free space does not establish free space inside Docker's virtual disk.

Projects: `Companion.Core`, `Companion.Data`, `Companion.Events`, `Companion.Messaging`, `Companion.Configuration`, `Services/Companion.Api`, `Companion.Extraction`, `Companion.Notification`, `Companion.Reporting`, `Companion.Realtime`, `Companion.Scheduler`, and `Tests/Companion.Api.Tests` (`SarsCompanion.slnx`). Key files: `Companion.Data/Models/ExpenseImage.cs`, `ExpenseImageBlob.cs`, `ExpenseExtraction.cs`, `CompanionDbContext.cs`, migrations `20261126142810_AddExtractionConstraints.cs` and `20261126153259_AddWorkspaceExpenseImages.cs`; `Companion.Core/Interfaces/IImageStore.cs`; `Companion.Data/Services/PostgresImageStore.cs`; `Services/Companion.Api/Features/Images/ImageEndpoints.cs` and `ImageHeader.cs`; `Features/Expenses/ExpenseCaptureEndpoints.cs` and `ExpenseExtractionEndpoints.cs`; `Services/Companion.Api/Services/OutboxPublisher.cs`; `Services/Companion.Extraction/ExtractionWorker.cs`, `ReceiptExtractionProcessor.cs`, `ReceiptRecoveryWorker.cs`, `Ocr/TesseractOcrEngine.cs`, and `Logic/` parser files. Relevant tests are `Tests/Companion.Api.Tests/{ImageApiTests,ImageStoreTests,ExtractionWorkerTests,ExtractionRabbitMqTests,ReceiptExtractionProcessorTests,ReceiptRecoveryWorkerTests,FieldParserTests,OutboxPublisherTests}.cs` and `web/tests/receipt-{capture,real}.spec.ts`.

### Step 1: build and tests

`dotnet build ../SarsCompanion.slnx -c Release`:

```text
  Determining projects to restore...
  All projects are up-to-date for restore.
  Companion.Configuration -> /Users/bubelebenkosifikeni/Documents/Projects/10th Stone/Companion/Companion.Configuration/bin/Release/net10.0/Companion.Configuration.dll
  Companion.Core -> /Users/bubelebenkosifikeni/Documents/Projects/10th Stone/Companion/Companion.Core/bin/Release/net10.0/Companion.Core.dll
  Companion.Events -> /Users/bubelebenkosifikeni/Documents/Projects/10th Stone/Companion/Companion.Events/bin/Release/net10.0/Companion.Events.dll
  Companion.Messaging -> /Users/bubelebenkosifikeni/Documents/Projects/10th Stone/Companion/Companion.Messaging/bin/Release/net10.0/Companion.Messaging.dll
  Companion.Notification -> /Users/bubelebenkosifikeni/Documents/Projects/10th Stone/Companion/Services/Companion.Notification/bin/Release/net10.0/Companion.Notification.dll
  Companion.Data -> /Users/bubelebenkosifikeni/Documents/Projects/10th Stone/Companion/Companion.Data/bin/Release/net10.0/Companion.Data.dll
  Companion.Realtime -> /Users/bubelebenkosifikeni/Documents/Projects/10th Stone/Companion/Services/Companion.Realtime/bin/Release/net10.0/Companion.Realtime.dll
  Companion.Reporting -> /Users/bubelebenkosifikeni/Documents/Projects/10th Stone/Companion/Services/Companion.Reporting/bin/Release/net10.0/Companion.Reporting.dll
  Companion.Scheduler -> /Users/bubelebenkosifikeni/Documents/Projects/10th Stone/Companion/Services/Companion.Scheduler/bin/Release/net10.0/Companion.Scheduler.dll
  Companion.Extraction -> /Users/bubelebenkosifikeni/Documents/Projects/10th Stone/Companion/Services/Companion.Extraction/bin/Release/net10.0/Companion.Extraction.dll
  Companion.Api -> /Users/bubelebenkosifikeni/Documents/Projects/10th Stone/Companion/Services/Companion.Api/bin/Release/net10.0/Companion.Api.dll
  Companion.Api.Tests -> /Users/bubelebenkosifikeni/Documents/Projects/10th Stone/Companion/Tests/Companion.Api.Tests/bin/Release/net10.0/Companion.Api.Tests.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:14.75
```

`dotnet test ../SarsCompanion.slnx -m:1 -nr:false`: exit 1; reported 221 passed, 14 failed, 0 skipped, 235 total. The execution tool truncated the full raw output. The reported failure contains `53100: No space left on device`, a `pg_wal/xlogtemp.1467` write failure and subsequent PostgreSQL `57P03` recovery/not-ready errors. The test run is **FAIL**, and the affected behavior is not verified. The full transcript was not captured in this report.

`PATH="$HOME/.nvm/versions/node/v22.14.0/bin:$PATH" npm run build`:

```text
> build
> vite build

error during build:
Error: EACCES: permission denied, rmdir '/Users/bubelebenkosifikeni/Documents/Projects/10th Stone/Companion/web/.output//public'
```

`npm run lint` with Node 22 exited 1: ESLint reported 102 errors and 7 warnings; the tool truncated its full output. First output:

```text
> lint
> eslint .

/Users/bubelebenkosifikeni/Documents/Projects/10th Stone/Companion/web/src/components/receipt-capture/service.ts
   83:8   error  Replace ...  prettier/prettier
```

The displayed last line above is abbreviated, not a verbatim copy of that long diagnostic. `./node_modules/.bin/tsc --noEmit` with Node 22 exited 0 with no output. Dependency installation was **not run**, to avoid modifying the checkout or installed dependencies.

`PLAYWRIGHT_CHANNEL=chrome ./node_modules/.bin/playwright test --config tests/receipt.playwright.config.ts --reporter=line` with Node 22:

```text
Running 21 tests using 2 workers
  21 passed (34.6s)
```

This is the exact summary, not the complete test listing. These browser tests use fake camera input and mocked gateway responses (`web/tests/receipt.playwright.config.ts:8-16`, `web/tests/receipt-real.spec.ts:170-197`). Scans of `Tests/*.cs` for `Fact`/`Theory` skip attributes or commented attributes, and `web/tests` for `test.skip`, `fixme`, `xit`, and commented tests found no matches. Other forms of disabled test remain unverified.

`git diff --name-status a776c44 HEAD -- Tests` and `git diff --name-status c6d97d2 HEAD -- tests` showed additions and modifications but no deleted test files. Individual removed or renamed test methods and test counts at those historical commits were not established; absence of deleted files does not establish absence of deleted tests.

### Step 2: schema

`ConnectionStrings__CompanionDb='Host=127.0.0.1;Port=1;Database=nonsecret_audit;Username=nonsecret;Password=nonsecret' dotnet ef migrations has-pending-model-changes --project Companion.Data/Companion.Data.csproj --startup-project Companion.Data/Companion.Data.csproj --no-build`:

```text
No changes have been made to the model since the last migration.
```

Focused PostgreSQL Testcontainers run, using the existing compiled tests, included `ImageStoreTests.Migration_FreshAndUpgrade_PreservesExistingProfilesAndStorage`, `ImageStoreTests.Database_RejectsInvalidMetadataAndBlob`, and `CompanionPersistenceTests.Schema_HasOnlyExpectedCheckConstraints`:

```text
  Passed Companion.Api.Tests.ImageStoreTests.Migration_FreshAndUpgrade_PreservesExistingProfilesAndStorage(upgrade: False)
  Passed Companion.Api.Tests.ImageStoreTests.Migration_FreshAndUpgrade_PreservesExistingProfilesAndStorage(upgrade: True)
  Passed Companion.Api.Tests.ImageStoreTests.Database_RejectsInvalidMetadataAndBlob
  Passed Companion.Api.Tests.CompanionPersistenceTests.Schema_HasOnlyExpectedCheckConstraints
Test Run Successful.
Total tests: 4
     Passed: 4
```

This is a shortened test transcript. The upgrade starts at `20261126142810_AddExtractionConstraints` before image storage, not a populated legacy image database. Tests asserted PostgreSQL catalog flags, but individual SQL query outputs were not printed. The migration declares `data bytea`, blob and expense cascade FKs, page and size checks, unique hash and `DEFERRABLE INITIALLY DEFERRED` page constraint, and `ALTER COLUMN data SET STORAGE EXTERNAL` (`Companion.Data/Migrations/20261126153259_AddWorkspaceExpenseImages.cs:74-173`). Extraction's unique hash and requested confidence columns are in `Companion.Data/Migrations/20261126142810_AddExtractionConstraints.cs:13-59`. Complete independent SQL column and constraint dumps, all FK behavior, and a whole-API integer-ID audit are **UNVERIFIED**.

### Steps 4 and 5: live smoke and performance

Full stack was not started; no synthetic multi-page capture, screenshots, field values, confidence values, outcome, or timings were obtained. `tesseract --version` returned:

```text
tesseract: command not found
```

This command alone does not establish whether the .NET native wrapper can run. Production web build failed with the exact error above; `lighthouse --version` returned:

```text
lighthouse: command not found
```

Lighthouse, initial and lazy chunk sizes, and camera code exclusion from the actual production initial bundle are **UNVERIFIED**. Synthetic images would not establish real-receipt OCR accuracy.

## 2. Requirement checklist

Source-based passes are limited to the inspected path; they are not claims of live end-to-end operation.

| # | Requirement | Result | Evidence / proving output |
|---|---|---|---|
| 1 | Separate bytea, interface, blob-free list | PASS | `IImageStore.cs:3-10`; `PostgresImageStore.cs:95-100,127-136`; migration `20261126153259_AddWorkspaceExpenseImages.cs:114-130`; focused schema tests above. No large-object use observed in this path. |
| 2 | Upload validation, signatures, SHA, duplicate, dimensions | PASS | `ImageEndpoints.cs:117-163`; `ImageHeader.cs:9-26,30-69,84-88,152-160`; `PostgresImageStore.cs:18-36`. Request cap includes 64 KiB multipart overhead; file stream limit is separately enforced. Live Kestrel limit unverified. |
| 3 | Ten images under transaction lock | PASS | `PostgresImageStore.cs:23-24,38-42,59-62,174-183`; concurrent test `ImageStoreTests.cs:130-147`. |
| 4 | Mutation status rule | PASS | `PostgresImageStore.cs:24-26,74-77,145-148,205`; `ImageEndpoints.cs:173-178`. Invalid input may be rejected before the status check. |
| 5 | Content headers and conditional GET | PASS | `ImageEndpoints.cs:21-23,58-71`; assertions `ImageApiTests.cs:116-133`. |
| 6 | JWT, membership and cross-tenant 404 | PASS | `AuthServiceCollectionExtensions.cs:64-104`; `ImageEndpoints.cs:42-57,72-110,173-175`; `PostgresImageStore.cs:162-184`. Whole endpoint matrix was not independently exercised. |
| 7 | Transactional swap, delete renumber | PASS | `PostgresImageStore.cs:78-90,145-157`; migration `20261126153259_AddWorkspaceExpenseImages.cs:123-130,172`. |
| 8 | Nonempty submit, atomic status/outbox, event payload | PASS | `ExpenseCaptureEndpoints.cs:92-145`; `Companion.Events/Events/ReceiptUploadedEvent.cs`; tests `ImageApiTests.cs:240-259,280-297`. |
| 9 | Disabled everywhere, no processing | UNVERIFIED | Gate in `Services/Companion.Extraction/Program.cs` and `ExtractionWorker.cs:18`; every deployment override and compose file was not exhaustively demonstrated. |
| 10 | Generic messaging only | UNVERIFIED | Worker subscription `ExtractionWorker.cs:22-24`; wrapper `Companion.Messaging/EasyNetQCompanionMessageBus.cs:7-30`. Exhaustive direct-dependency and new-helper scan not recorded. |
| 11 | Duplicate and ordered hash | UNVERIFIED | Claim/fencing `ReceiptExtractionProcessor.cs:89-165,200-205`; independently proving all duplicate and ordered-hash cases requires further test evidence. |
| 12 | Enforceable OCR timeout | FAIL | `TesseractOcrEngine.cs:45-56` calls native OCR synchronously; cancellation cannot interrupt a hung call. Default timeout is 120 seconds; lease threshold is 540 seconds (`appsettings.json:8-13`, `ReceiptExtractionProcessor.cs:107-130`), so the nominal timeout is below half the threshold but does not stop a hang. A hung call can block indefinitely. |
| 13 | Sweeper, retry cap, safe recovery | UNVERIFIED | Thirty-second sweep (`ReceiptRecoveryWorker.cs:15-16,21-38`), default three attempts (`appsettings.json:8-13`), failed state (`ReceiptExtractionProcessor.cs:279-304`). Complete broker redelivery/idempotency and stale recovery were not independently demonstrated. |
| 14 | No overwrite of manual fields | UNVERIFIED | `ReceiptExtractionProcessor.cs:195-233` writes extraction results. No test proving protection for every `User_Manual`/`User_Override` field was established. |
| 15 | Parser rules | UNVERIFIED | Relevant units are `Logic/{VatAmountExtractor,VatClassifier,TaxInvoiceDetector,DateParser,VendorExtractor}.cs`; full rule matrix was not independently proven by a targeted passing run. |
| 16 | Preprocessing and packaged eng+afr | FAIL | Direct processing in `Ocr/TesseractOcrEngine.cs:14-22,53-56`; no complete orientation/grayscale/contrast/downscale pipeline or tracked language data/application Dockerfile was found; `docker-compose.yml:3`. Actual installed language data unverified. |
| 17 | Narrow extraction DB role | FAIL | Shared connection in `Program.cs:28-31`; no checked-in extraction role/grant SQL was found. Actual deployment grants unverified. |
| 18 | Privacy-safe explicit logging | UNVERIFIED | Log inventory below; no listed template explicitly contains OCR text, image bytes, file names, vendor, amounts or VAT numbers. Framework/native logging and exception contents were not exhaustively demonstrated. |
| 19 | Abandoned server draft cleanup | FAIL | Expense creation `ExpenseCaptureEndpoints.cs:73-82`; `Companion.Data/Models/Expense.cs:3-10` has no expiry timestamp; no cleanup path found. External cleanup is unverified. |
| 20a | Lazy capture import | PASS | `web/src/routes/_app/capture.image.tsx:13-16,47-62`; actual production bundle unverified. |
| 20b | Camera/file fallback | PASS | `camera.ts:69-77,127-134`; `ReceiptCapture.tsx:392-406,477-524`; Playwright fake-camera suite passed. |
| 20c | Preview before save | PASS | `ReceiptCapture.tsx:409-456,596-613`. |
| 20d | Ten-image cap | PASS | `config.ts:2-4`; `ReceiptCapture.tsx:180-196`; Playwright suite passed. |
| 20e | WebP-to-JPEG fallback | PASS | `images.ts:77-90`. |
| 20f | EXIF removal | PASS | `images.ts:63-74,93-110,120-130`; EXIF/GPS Playwright case passed. |
| 20g | IndexedDB drafts | PASS | `draft.ts:6-35,39-64`; `useDraft.ts:14-29,37-53`; browser suite passed. |
| 20h | Object URLs revoked | PASS | `ReceiptCapture.tsx:62-67`; `images.ts:93-110`; `UploadedImageGallery.tsx:141-162`. |
| 20i | Capture states | PASS | `ReceiptCapture.tsx:327-357,526-593`; `useReceiptSave.ts:139-180`; browser suite passed. |
| 20j | Accessibility | UNVERIFIED | Controls and dialogs `ReceiptCapture.tsx:328-345,444-451,774-789`, `ReceiptDialog.tsx:25-44`; automated browser suite passed, real assistive technology and devices not tested. |
| 21 | Authenticated gallery/review image retrieval | FAIL | Gallery bearer-token blob fetch and cleanup `web/src/components/UploadedImageGallery.tsx:119-162`, `web/src/lib/auth-api.ts:128-139,180-182`; `ExtractionReview.tsx:51-68,133-213` does not display or fetch an image. |
| 22 | No image data in local/session storage | PASS | Static source audit: drafts use IndexedDB `draft.ts:6-35`; observed web-storage writes are theme and demo markers (`use-theme.ts:14-16`, `mock-session.ts:13-18`). Runtime storage not independently inspected. |
| 23 | No forbidden UI claims | UNVERIFIED | `ExtractionReview.tsx:144-146` says suggestions are not approved; full product-wide UI string audit not completed. |

### Explicit logging inventory reviewed

- `Services/Companion.Extraction/Program.cs:43,45`: startup and disabled status.
- `Services/Companion.Extraction/ExtractionWorker.cs:18,24,33,43,46,54`: disabled, subscribed, invalid event, start, handler completion, failure type.
- `Services/Companion.Extraction/ReceiptExtractionProcessor.cs:43,66,235`: processing/recovery failure type and page count.
- `Services/Companion.Extraction/ReceiptRecoveryWorker.cs:36,83`: sweep/item failure type.
- `Services/Companion.Extraction/Ocr/TesseractOcrEngine.cs:49,64,70,75`: start, counts/timing, cancellation, failure type.
- No explicit logging statements found in image or capture endpoint files. Outbox statements are `Services/Companion.Api/Services/OutboxPublisher.cs:39,89,91,98`; the first three attach exceptions, whose contents were not established safe. Framework/native logs are not covered by this inventory.

## 3. Mutation checks

No source or test mutation was made. Every requested mutation is **UNVERIFIED**, not a coverage pass or fail; consequently the requested mutation-based coverage assessment was not completed.

Final `git -C .. status --short && git -C .. diff --stat && git -C .. diff --check && git status --short && git diff --stat && git diff --check` from `web` exited 0:

```text
?? docs/mvp-capture-extraction-audit.md
```

The requested report is the only reported working-tree change; `git diff` does not display untracked file content.

| Mutation | Test result |
|---|---|
| a Remove content workspace check | UNVERIFIED — not run |
| b Change limit 10 to 11 | UNVERIFIED — not run |
| c Remove magic-byte validation | UNVERIFIED — not run |
| d Return error on duplicate hash | UNVERIFIED — not run |
| e Remove status 409 | UNVERIFIED — not run |
| f Move outbox outside transaction | UNVERIFIED — not run |
| g Ignore extraction enabled flag | UNVERIFIED — not run |
| h Overwrite manual fields | UNVERIFIED — not run |
| i Derive absent VAT | UNVERIFIED — not run |
| j Remove camera `track.stop()` | UNVERIFIED — not run |

## 4. Defects

### Blocking by impact

1. **FAIL:** Native OCR cannot be interrupted during a hang, so the nominal timeout does not bound work (`TesseractOcrEngine.cs:45-56`).
2. **FAIL:** Required preprocessing and deployable English/Afrikaans OCR data were not found (`TesseractOcrEngine.cs:14-22,53-56`; `docker-compose.yml:3`).
3. **FAIL:** No checked-in narrowly scoped extraction database grants (`Services/Companion.Extraction/Program.cs:28-31`).
4. **FAIL:** No abandoned server-draft expiry or cleanup path found (`ExpenseCaptureEndpoints.cs:73-82`; `Expense.cs:3-10`).
5. **FAIL:** Whole-solution test run failed with PostgreSQL disk errors; frontend production build and lint failed as recorded above.

### Other findings

- Review does not display the receipt image (`web/src/components/receipt-capture/ExtractionReview.tsx:51-68,133-213`).
- The standalone capture document and architecture describe presigned R2, whereas the implementation uses multipart PostgreSQL storage (`web/receipt-capture.md:21-26`; `Architecture.md:133-137`; `ImageEndpoints.cs:113-160`).
- The capture document claims an automatic enlarged review and an obsolete stub (`web/receipt-capture.md:3,15`); current behavior is visible in `ReceiptCapture.tsx:409-456,596-613` and `service.ts:78-120`.

## 5. Not verified

- Full raw test and lint transcripts, method-level historical test-count comparison, explicit dependency installation, exhaustive skipped-test search, whole-API ID exposure, all schema catalog outputs and populated legacy upgrades.
- Full live stack, real native OCR, three-image synthetic smoke, screenshot evidence, processing outcome and timings. No claim is made about real-receipt OCR accuracy.
- Lighthouse, production bundle sizes, all requested mutation checks, and physical-device behavior.
- Automatic provision of required workspace memberships and permission grants; live role configuration; the complete review/approval workflow and qualification lifecycle.

## 6. Contract gaps

The inspected `web/receipt-capture.md:21-26` describes request-target/presigned PUT and confirmation; `web/src/components/receipt-capture/service.ts:80-120` instead initializes an expense, multipart-posts images and submits it: **MISMATCH**. `Architecture.md:133-137,246-248` also describes R2: **MISMATCH** with PostgreSQL storage. Gateway multipart fields and response metadata are at `ImageEndpoints.cs:135-163`; client calls and mapping are at `service.ts:80-120`: route, authentication, `file`, `label`, `pageNo`, content type, result and error-shape compatibility are **UNVERIFIED** against a current authoritative service-contract document and live server. Review displays suggestions only (`ExtractionReview.tsx:144-146`); persistence of corrections or approval is **UNVERIFIED/not implemented in this view**. Status and lost-response handling appear in `useReceiptSave.ts:74-137` but live transitions remain **UNVERIFIED**.

## 7. Manual owner checklist

All **NOT DONE**: test camera permission and denial, native/file fallback, orientation and image types, preview/edit/order, accessibility, offline draft/reload/retry, cancellation, authenticated gallery and review, and capture-to-extraction status on each:

- iPhone Safari and installed PWA — NOT DONE
- Android Chrome — NOT DONE
- Samsung Internet — NOT DONE
- In-app browser — NOT DONE
- Windows Chrome, Edge and Firefox — NOT DONE
- macOS Safari and Chrome — NOT DONE
- ChromeOS — NOT DONE