﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
#if WPF
using System.Reactive.Linq;
using System.Windows;
using Microsoft.Win32;
#else
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI.Popups;
using Windows.UI.Xaml.Controls;
#endif

namespace H.ReactiveUI;

public class InteractionManager
{
    #region Properties

    private Func<string, string>? LocalizationFunc { get; }

#if !WPF
    private Dictionary<string, StorageFile> StorageFiles { get; } = new();
#endif

    #endregion

    #region Constructors

    public InteractionManager(Func<string, string>? localizationFunc = null)
    {
        LocalizationFunc = localizationFunc;
    }

    #endregion

    #region Methods

    private string Localize(string value) => LocalizationFunc?.Invoke(value) ?? value;

    public void Register()
    {
#if WPF
        _ = MessageInteractions.Message.RegisterHandler(context =>
        {
            var message = context.Input;

            message = Localize(message);

            Trace.WriteLine($"Message: {message}");
            _ = MessageBox.Show(
                message,
                "Message:",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            context.SetOutput(Unit.Default);
        });
        _ = MessageInteractions.Warning.RegisterHandler(context =>
        {
            var warning = context.Input;

            warning = Localize(warning);

            Trace.WriteLine($"Warning: {warning}");
            _ = MessageBox.Show(
                warning,
                "Warning:",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            context.SetOutput(Unit.Default);
        });
        _ = MessageInteractions.Exception.RegisterHandler(static context =>
        {
            var exception = context.Input;

            Trace.WriteLine($"Exception: {exception}");
            _ = MessageBox.Show(
                $"{exception}",
                "Exception:",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            context.SetOutput(Unit.Default);
        });
        _ = MessageInteractions.Question.RegisterHandler(context =>
        {
            var question = context.Input;

            var message = Localize(question.Message);
            var title = Localize(question.Title);
            var body = message;
            if (!string.IsNullOrWhiteSpace(question.AdditionalData))
            {
                body += Environment.NewLine + question.AdditionalData;
            }

            Trace.WriteLine($@"Question: {title}
{body}");

            var result = MessageBox.Show(
                body,
                title,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);

            context.SetOutput(result == MessageBoxResult.Yes);
        });
        _ = FileInteractions.OpenFile.RegisterHandler(context =>
        {
            var (extensions, filterName) = context.Input;

            var wildcards = extensions
                .Select(static extension => $"*{extension}")
                .ToArray();
            var filter = $@"{Localize(filterName)} ({string.Join(", ", wildcards)})|{string.Join(";", wildcards)}";

            var dialog = new OpenFileDialog
            {
                CheckFileExists = true,
                CheckPathExists = true,
                Filter = filter,
            };
            if (dialog.ShowDialog() != true)
            {
                context.SetOutput(new(string.Empty, string.Empty, Array.Empty<byte>()));
                return;
            }

            var path = dialog.FileName;
            var model = path.ToFile();

            context.SetOutput(model);
        });
        _ = FileInteractions.OpenFiles.RegisterHandler(context =>
        {
            var (extensions, filterName) = context.Input;

            var wildcards = extensions
                .Select(static extension => $"*{extension}")
                .ToArray();
            var filter = $@"{Localize(filterName)} ({string.Join(", ", wildcards)})|{string.Join(";", wildcards)}";

            var dialog = new OpenFileDialog
            {
                CheckFileExists = true,
                CheckPathExists = true,
                Filter = filter,
                Multiselect = true,
            };
            if (dialog.ShowDialog() != true)
            {
                context.SetOutput(Array.Empty<FileData>());
                return;
            }

            var paths = dialog.FileNames;
            var models = paths
                .Select(static path => path.ToFile())
                .ToArray();

            context.SetOutput(models);
        });
        _ = FileInteractions.SaveFile.RegisterHandler(static context =>
        {
            var (fileName, extension, bytes) = context.Input;

            var dialog = new SaveFileDialog
            {
                FileName = fileName,
                DefaultExt = extension,
                AddExtension = true,
            };
            if (dialog.ShowDialog() != true)
            {
                context.SetOutput(null);
                return;
            }

            var path = dialog.FileName;

            File.WriteAllBytes(path, bytes);

            context.SetOutput(path);
        });
        _ = FileInteractions.LaunchPath.RegisterHandler(static context =>
        {
            var path = context.Input;

            _ = Process.Start(new ProcessStartInfo(path)
            {
                UseShellExecute = true,
            });

            context.SetOutput(Unit.Default);
        });
        _ = FileInteractions.LaunchInTemp.RegisterHandler(async static context =>
        {
            var (fileName, extension, bytes) = context.Input;

            var folder = Path.Combine(
                Path.GetTempPath(),
                "H.ReactiveUI.CommonInteractions",
                $"{new Random().Next()}");
            var path = Path.Combine(
                folder,
                $"{fileName}{extension}");

            _ = Directory.CreateDirectory(folder);
            File.WriteAllBytes(path, bytes);

            _ = await FileInteractions.LaunchPath.Handle(path);

            context.SetOutput(Unit.Default);
        });
        _ = FileInteractions.LaunchFolder.RegisterHandler(static context =>
        {
            var path = context.Input;

            _ = Process.Start(new ProcessStartInfo(path)
            {
                UseShellExecute = true,
            });

            context.SetOutput(Unit.Default);
        });
        _ = WebInteractions.OpenUrl.RegisterHandler(static context =>
        {
            var url = context.Input;

            _ = Process.Start(new ProcessStartInfo(url)
            {
                UseShellExecute = true,
            });

            context.SetOutput(Unit.Default);
        });
#else
        _ = MessageInteractions.Message.RegisterHandler(async context =>
        {
            var message = context.Input;

            message = Localize(message);

            Trace.WriteLine($"Message: {message}");
            var dialog = new MessageDialog(message, "Message:");

            context.SetOutput(Unit.Default);

            _ = await dialog.ShowAsync();
        });
        _ = MessageInteractions.Warning.RegisterHandler(async context =>
        {
            var warning = context.Input;

            warning = Localize(warning);

            Trace.WriteLine($"Warning: {warning}");
            var dialog = new MessageDialog(warning, "Warning:");

            context.SetOutput(Unit.Default);

            await dialog.ShowAsync();
        });
        _ = MessageInteractions.Exception.RegisterHandler(static async context =>
        {
            var exception = context.Input;

            Trace.WriteLine($"Exception: {exception}");
            var dialog = new MessageDialog($"{exception}", "Exception:");

            context.SetOutput(Unit.Default);

            await dialog.ShowAsync();
        });
        _ = MessageInteractions.Question.RegisterHandler(async context =>
        {
            var question = context.Input;

            var message = Localize(question.Message);
            var title = Localize(question.Title);
            var body = message;
            if (!string.IsNullOrWhiteSpace(question.AdditionalData))
            {
                body += Environment.NewLine + question.AdditionalData;
            }

            Trace.WriteLine($@"Question: {title}
{body}");

            var dialog = new ContentDialog
            {
                Title = title,
                Content = body,
                PrimaryButtonText = "Yes",
                CloseButtonText = "No",
            };
            var result = await dialog.ShowAsync();

            context.SetOutput(result == ContentDialogResult.Primary);
        });
        _ = FileInteractions.OpenFile.RegisterHandler(static async context =>
        {
            var (extensions, filterName) = context.Input;

            var picker = new FileOpenPicker();
            foreach (var extension in extensions)
            {
                picker.FileTypeFilter.Add(extension);
            }

            var file = await picker.PickSingleFileAsync();
            if (file == null)
            {
                context.SetOutput(new(string.Empty, string.Empty, Array.Empty<byte>()));
                return;
            }

            var model = await file.ToFileAsync().ConfigureAwait(false);

            context.SetOutput(model);
        });
        _ = FileInteractions.OpenFiles.RegisterHandler(static async context =>
        {
            var (extensions, filterName) = context.Input;

            var picker = new FileOpenPicker();
            foreach (var extension in extensions)
            {
                picker.FileTypeFilter.Add(extension);
            }

            var files = await picker.PickMultipleFilesAsync();

            var models = await Task.WhenAll(files
                .Select(static file => file.ToFileAsync())).ConfigureAwait(false);

            context.SetOutput(models);
        });
        _ = FileInteractions.SaveFile.RegisterHandler(async context =>
        {
            var (fileName, extension, bytes) = context.Input;

            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.Downloads,
                SuggestedFileName = fileName,
                FileTypeChoices =
                {
                        { extension, new List<string> { extension } },
                },
            };
            var file = await picker.PickSaveFileAsync();
            if (file == null)
            {
                context.SetOutput(null);
                return;
            }

            using (var stream = await file.OpenStreamForWriteAsync().ConfigureAwait(false))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(bytes);
            }

            StorageFiles[file.Path] = file;

            context.SetOutput(file.Path);
        });
        _ = FileInteractions.LaunchPath.RegisterHandler(async context =>
        {
            var path = context.Input;

            var file = StorageFiles.TryGetValue(path, out var result)
                ? result
                : await StorageFile.GetFileFromPathAsync(path);

            _ = await Launcher.LaunchFileAsync(file);

            context.SetOutput(Unit.Default);
        });
        _ = FileInteractions.LaunchInTemp.RegisterHandler(async static context =>
        {
            var (fileName, extension, bytes) = context.Input;

            var file = await ApplicationData.Current.TemporaryFolder
                .CreateFileAsync($"{fileName}{extension}", CreationCollisionOption.ReplaceExisting);

            using (var stream = await file.OpenStreamForWriteAsync().ConfigureAwait(true))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(bytes);
            }

            _ = await Launcher.LaunchFileAsync(file);

            context.SetOutput(Unit.Default);
        });
        _ = FileInteractions.LaunchFolder.RegisterHandler(static async context =>
        {
            var path = context.Input;

            var folder = await StorageFolder.GetFolderFromPathAsync(path);

            _ = await Launcher.LaunchFolderAsync(folder);

            context.SetOutput(Unit.Default);
        });
        _ = WebInteractions.OpenUrl.RegisterHandler(static async context =>
        {
            var url = context.Input;

            _ = await Launcher.LaunchUriAsync(new Uri(url))
#if Uno
                .ConfigureAwait(false)
#endif
                ;

            context.SetOutput(Unit.Default);
        });
#endif

        #endregion
    }
}
