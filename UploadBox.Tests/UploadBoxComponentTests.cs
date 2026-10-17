using Bunit;
using AngleSharp.Dom;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Forms;
using UploadBox.Models;
using UploadBox.Services;
using UploadComponent = UploadBox.Pages.UploadBox;

namespace UploadBox.Tests;

/// <summary>
/// Tests upload component rendering, file selection, drag-and-drop, and controller events.
/// </summary>
public class UploadBoxComponentTests : BunitContext
{
    /// <summary>
    /// Tests render requires controller parameter.
    /// </summary>
    [Test]
    public void Render_RequiresControllerParameter()
    {
        Assert.Throws<InvalidOperationException>(() => Render<UploadComponent>());
    }

    /// <summary>
    /// Tests render multiple selection shows drop instructions and enables multiple files.
    /// </summary>
    [Test]
    public void Render_MultipleSelection_ShowsDropInstructionsAndEnablesMultipleFiles()
    {
        UploadBoxController controller = new(new UploadOptions(maxFileCount: 2));

        var cut = Render<UploadComponent>(parameters => parameters.Add(component => component.Controller, controller));
        IElement input = cut.Find("input[type=file]");

        Assert.Multiple(() =>
        {
            Assert.That(cut.Markup, Does.Contain("Drop files here"));
            Assert.That(cut.Markup, Does.Contain("or click to browse"));
            Assert.That(input.HasAttribute("multiple"), Is.True);
        });
    }

    /// <summary>
    /// Tests render single selection shows drop instructions and restricts to single file.
    /// </summary>
    [Test]
    public void Render_SingleSelection_ShowsDropInstructionsAndRestrictsToSingleFile()
    {
        UploadBoxController controller = new(new UploadOptions(maxFileCount: 1));

        var cut = Render<UploadComponent>(parameters => parameters.Add(component => component.Controller, controller));
        IElement input = cut.Find("input[type=file]");

        Assert.Multiple(() =>
        {
            Assert.That(cut.Markup, Does.Contain("Drop a file here"));
            Assert.That(cut.Markup, Does.Contain("or click to browse"));
            Assert.That(input.HasAttribute("multiple"), Is.False);
        });
    }

    /// <summary>
    /// Tests drag enter and leave toggle drag over class until all drags leave.
    /// </summary>
    [Test]
    public void DragEnterAndLeave_ToggleDragOverClassUntilAllDragsLeave()
    {
        UploadBoxController controller = new();
        var cut = Render<UploadComponent>(parameters => parameters.Add(component => component.Controller, controller));
        IElement dropZone = cut.Find("label");

        dropZone.TriggerEvent("ondragenter", new DragEventArgs());
        Assert.That(dropZone.ClassList.Contains("drag-over"), Is.True);

        dropZone.TriggerEvent("ondragenter", new DragEventArgs());
        dropZone.TriggerEvent("ondragleave", new DragEventArgs());
        Assert.That(dropZone.ClassList.Contains("drag-over"), Is.True);

        dropZone.TriggerEvent("ondragleave", new DragEventArgs());
        Assert.That(dropZone.ClassList.Contains("drag-over"), Is.False);
    }

    /// <summary>
    /// Tests drop clears drag over state.
    /// </summary>
    [Test]
    public void Drop_ClearsDragOverState()
    {
        UploadBoxController controller = new();
        var cut = Render<UploadComponent>(parameters => parameters.Add(component => component.Controller, controller));
        IElement dropZone = cut.Find("label");
        dropZone.TriggerEvent("ondragenter", new DragEventArgs());

        dropZone.TriggerEvent("ondrop", new DragEventArgs());

        Assert.That(dropZone.ClassList.Contains("drag-over"), Is.False);
    }

    /// <summary>
    /// Tests file selection marks controller and invokes upload callback with all files.
    /// </summary>
    [Test]
    public void FileSelection_MarksControllerAndInvokesUploadCallbackWithAllFiles()
    {
        UploadBoxController controller = new(new UploadOptions(maxFileCount: 2));
        UploadRequest? requested = null;
        var cut = Render<UploadComponent>(parameters => parameters
            .Add(component => component.Controller, controller)
            .Add(component => component.UploadRequested, request => requested = request));
        var input = cut.FindComponent<InputFile>();

        input.UploadFiles(
            InputFileContent.CreateFromText("first", "first.iso"),
            InputFileContent.CreateFromText("second", "second.iso"));

        Assert.Multiple(() =>
        {
            Assert.That(controller.Status, Is.EqualTo(UploadBoxStatus.FileSelected));
            Assert.That(controller.StatusMessage, Is.EqualTo("2 file(s) selected."));
            Assert.That(requested, Is.Not.Null);
            Assert.That(requested!.FileCount, Is.EqualTo(2));
            Assert.That(requested.Files.Select(file => file.Name), Is.EqualTo(new[] { "first.iso", "second.iso" }));
        });
    }

    /// <summary>
    /// Tests controller state change renders component and dispose unsubscribes.
    /// </summary>
    [Test]
    public void ControllerStateChange_RendersComponentAndDisposeUnsubscribes()
    {
        UploadBoxController controller = new();
        var cut = Render<UploadComponent>(parameters => parameters.Add(component => component.Controller, controller));
        int renderCount = cut.RenderCount;

        controller.MarkFilesSelected(1);
        cut.WaitForAssertion(() => Assert.That(cut.RenderCount, Is.GreaterThan(renderCount)));
        int renderCountAfterNotification = cut.RenderCount;

        cut.Dispose();
        controller.MarkFilesSelected(2);

        Assert.That(cut.RenderCount, Is.EqualTo(renderCountAfterNotification));
    }
}
