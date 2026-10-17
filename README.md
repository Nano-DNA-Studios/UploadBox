# UploadBox

UploadBox is a reusable Blazor file upload component and supporting .NET library. It provides a browser file picker with drag-and-drop interaction, configurable file validation, upload progress and cancellation support, and staged file writes to a server-selected destination.

The library does not define an application host, storage policy, or upload endpoint. Your application handles the upload callback and chooses where accepted files are stored.

## Features

- Blazor file picker with drag-and-drop interaction.
- Single-file and multiple-file selection.
- Configurable maximum file count and file size.
- Optional file-extension restriction.
- Strict validation or a policy that skips invalid files.
- Per-file and aggregate batch progress models.
- Cancellation through standard .NET cancellation tokens.
- Staged writes with size verification and typed success, cancellation, and failure results.
- State-change notifications for updating application UI.

## Requirements

- .NET 9.
- An ASP.NET Core Blazor application.
- The ASP.NET Core shared framework (`Microsoft.AspNetCore.App`).

## Installation

### Add the project to your solution

Clone the repository and reference the library project from your application:

```bash
git clone https://github.com/Nano-DNA-Studios/UploadBox.git
```

Add `UploadBox/UploadBox.csproj` as a project reference, or build the solution from the repository root:

```bash
dotnet build UploadBox.sln
```

The release workflow is configured to publish packages to GitHub Packages. See the repository's Releases page and package instructions for the version and access requirements available to your account.

## Basic usage

The component raises an `UploadRequest` when the user selects files. Validate the selection, then handle accepted files in your application. The example below shows the component wiring and a minimal callback; choose and secure the destination directory according to your application.

```razor
@using UploadBox.Models
@using UploadBox.Pages
@using UploadBox.Services

<UploadBox Controller="@Controller" UploadRequested="@HandleUploadRequested" />

@code {
    private readonly UploadBoxController Controller = new(
        new UploadOptions(
            maxFileCount: 5,
            maxFileSizeBytes: 10 * 1024 * 1024,
            policy: UploadPolicy.SkipInvalidFiles,
            fileExtension: ".pdf"));

    private async Task HandleUploadRequested(UploadRequest request)
    {
        UploadValidationResult validation = Controller.ValidateFiles(request.Files);

        if (!validation.CanUpload)
        {
            // Show validation.Message in your application UI.
            return;
        }

        string destinationDirectory = Path.Combine(AppContext.BaseDirectory, "Uploads");
        Directory.CreateDirectory(destinationDirectory);

        foreach (UploadFileValidationResult file in validation.Files.Where(file => file.IsAccepted))
        {
            UploadFileResult result = await Controller.WriteUploadFile(
                file.File,
                Path.GetFileName(file.FileName),
                destinationDirectory,
                cancellationToken: CancellationToken.None);

            switch (result)
            {
                case UploadSucceeded success:
                    // Record or display success.File.SavedPath and success.Stats.
                    break;
                case UploadCancelled:
                case UploadFailed:
                    // Record or display the result for this file.
                    break;
            }
        }
    }
}
```

`UploadBoxController` exposes `StateChanged`, `Status`, `StatusMessage`, `Progress`, and `IsUploading` so the containing application can present status and progress. Unsubscribe from `StateChanged` when the owning component is disposed.

## Configuration

`UploadOptions` accepts these settings:

| Option | Default | Description |
|---|---:|---|
| `maxFileCount` | `1` | Maximum number of accepted files in a selection. Values above one enable multiple-file selection. |
| `maxFileSizeBytes` | `1,048,576` | Maximum accepted size for each file, in bytes. |
| `policy` | `UploadPolicy.Strict` | `Strict` blocks a selection containing rejected files; `SkipInvalidFiles` allows accepted files to proceed while invalid files are skipped. |
| `fileExtension` | none | Optional required extension, with or without a leading period. Matching is case-insensitive. |
| `bufferSize` | `1,048,576` | Buffer size in bytes used while staging each upload. |

Invalid option values (such as a non-positive count, size, or buffer) throw `ArgumentOutOfRangeException` when `UploadOptions` is created.

## Upload results and progress

`WriteUploadFile` returns an `UploadFileResult`:

- `UploadSucceeded` includes the saved file details and byte/overwrite statistics.
- `UploadCancelled` indicates that cancellation stopped the current transfer.
- `UploadFailed` includes a failure code and user-facing message.

For sequential batch uploads, use `UploadBatchProgressContext` to provide the current file index, accepted file count, bytes completed before the current file, and total batch size. Each `UploadProgress` update includes both file-level and aggregate byte counts.

## Security and storage notes

- Enforce authorization and any application-specific policy in the host application; the component is not an authorization boundary.
- Select destination directories on the server. Do not trust a client-provided path or filename as a storage policy.
- The controller validates file size, optional extension, and the final byte count. Apply any additional content inspection your application requires.
- Uploads are staged in the destination directory and moved into place after transfer validation. If the target file already existed when the upload was prepared, it can be replaced; decide whether that behavior is suitable for your application.
- Configure size limits at the Blazor/server hosting layers as appropriate for your deployment, in addition to `UploadOptions`.
- The library contains component markup but does not ship a stylesheet. Style the `upload-drop-zone`, `upload-input`, `upload-title`, and `upload-subtitle` classes in your application as needed.

# License

This project is available under the NanoDNA Community License 1.0.

Individuals may use and modify the project for personal, hobby, educational, or research purposes outside the work or operations of a business or institution. Qualifying small businesses may use and modify it for their own project if they have no more than 20 people and no more than USD $10,000 in project revenue during the preceding rolling 12-month period.

Businesses that exceed either threshold and institutional users must obtain separate written authorization from NanoDNA Studios. Redistribution or providing the software, modified versions, or derivative works to another person or entity also requires separate written authorization for each recipient.

For licensing requests, contact: Mr.DNAlexandre@gmail.com

This is a source-available license. It is not an OSI-approved open-source license.