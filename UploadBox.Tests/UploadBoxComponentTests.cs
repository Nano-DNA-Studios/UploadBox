using Bunit;
using AngleSharp.Dom;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Forms;
using UploadBox.Models;
using UploadBox.Services;
using UploadComponent = UploadBox.Pages.UploadBox;

namespace UploadBox.Tests;

public class UploadBoxComponentTests : BunitContext
{
    [Test]
    public void Render_RequiresControllerParameter()
    {
        Assert.Throws<InvalidOperationException>(() => Render<UploadComponent>());
    }

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
