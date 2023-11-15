using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.Fonts;
using SixLabors.ImageSharp.Formats.Jpeg;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Image = SixLabors.ImageSharp.Image;


namespace MyImageProcessor2;

public partial class MainWindow : Window
{
    private string photosFolder;
    private string outputFolder;
    private string textFilePath;

    public MainWindow()
    {
        InitializeComponent();
    }


    private async void On_PhotosFolderBrowseClicked(object sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Photos Folder"
        });

        if (folders.Count >= 1)
        {
            var uri = new Uri(folders[0].Path.ToString());
            PhotosFolderTextBox.Text = uri.LocalPath;
        }
    }

    private async void On_OutputFolderBrowseClicked(object sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Output Folder"
        });

        if (folders.Count >= 1)
        {
            var uri = new Uri(folders[0].Path.ToString());
            OutputFolderTextBox.Text = uri.LocalPath;
        }
    }

    private async void On_TextFileBrowseClicked(object sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Text File",
            AllowMultiple = false
        });

        if (files.Count >= 1)
        {
            var uri = new Uri(files[0].Path.ToString());
            TextFileTextBox.Text = uri.LocalPath;
        }
    }

    private void ProcessImagesClicked(object sender, RoutedEventArgs e)
        {
            // Retrieve the paths from the TextBoxes
            photosFolder = PhotosFolderTextBox.Text;
            outputFolder = OutputFolderTextBox.Text;
            textFilePath = TextFileTextBox.Text;

            // Run the image processing logic on a separate thread to keep the UI responsive
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                try
                {
                    Console.WriteLine("Processing images...");

                    // Image processing logic
                    var texts = new Dictionary<string, string>();
                    string[] lines = File.ReadAllLines(textFilePath, Encoding.UTF8);
                    foreach (string line in lines)
                    {
                        string[] parts = line.Split(new[] { ": " }, StringSplitOptions.None);
                        if (parts.Length == 2)
                        {
                            texts.Add(parts[0], parts[1]);
                        }
                    }

                    // Define horizontal A4 size in pixels (assuming 300 DPI)
                    Size a4Size = new Size(3508, 2480);

                    Parallel.ForEach(texts, item =>
                    {
                        string imagePath = Path.Combine(photosFolder, item.Key) + ".png";
                        string text = item.Value;

                        using (Image<Rgba32> image = Image.Load<Rgba32>(imagePath))
                        {
                            int dpi = 300;
                            float baseFontSize = 25; // Base font size for 150 DPI
                            float scaleFactor = dpi / 150f;
                            float calculatedFontSize = baseFontSize * scaleFactor;

                            var font = SystemFonts.CreateFont("Arial", calculatedFontSize);

                            // Wrap the text and measure its size
                            string wrappedText = WrapText(text, font, a4Size.Width - 50);
                            FontRectangle wrappedTextSize = TextMeasurer.MeasureBounds(wrappedText, new RendererOptions(font));

                            // Determine the available height for the image
                            int availableImageHeight = a4Size.Height - (int)wrappedTextSize.Height - 75; // 75 for margins
                            Size newSize = new Size(a4Size.Width, availableImageHeight);

                            using (Image<Rgba32> a4Image = new Image<Rgba32>(Configuration.Default, a4Size.Width, a4Size.Height, Color.White))
                            {
                                a4Image.Mutate(ctx =>
                                {
                                    ctx.DrawImage(image.Clone(ic => ic.Resize(newSize)), new Point(0, 0), 1);

                                    // Wrap the text manually
                                    var wrappedTextLines = WrapText(text, font, a4Size.Width - 50).Split(new[] { Environment.NewLine }, StringSplitOptions.None);

                                    // Draw each line, centering it horizontally
                                    float textY = newSize.Height + 25;
                                    foreach (var line in wrappedTextLines)
                                    {
                                        var textSize = TextMeasurer.MeasureSize(line, new RendererOptions(font));
                                        float textX = (a4Size.Width - textSize.Width) / 2;

                                        ctx.DrawText(line, font, Color.Black, new PointF(textX, textY));
                                        textY += textSize.Height; // Move to the next line
                                    }
                                });

                                string outputPath = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(item.Key) + ".jpg");

                                var options = new JpegEncoder
                                {
                                    Quality = 85
                                };

                                a4Image.Save(outputPath, options);
                            }
                        }
                    });

                    Console.WriteLine("All images processed!");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            });
        }

        private static string WrapText(string text, Font font, float maxWidth)
        {
            var words = text.Split(' ');
            var wrappedText = new StringBuilder();
            var currentLine = new StringBuilder();

            foreach (var word in words)
            {
                var potentialLine = currentLine.Length > 0 ? currentLine.ToString() + " " + word : word;
                var potentialLineWidth = TextMeasurer.MeasureSize(potentialLine, new RendererOptions(font)).Width;

                if (potentialLineWidth < maxWidth)
                {
                    currentLine.Append(currentLine.Length > 0 ? " " + word : word);
                }
                else
                {
                    wrappedText.AppendLine(currentLine.ToString());
                    currentLine.Clear().Append(word);
                }
            }

            wrappedText.Append(currentLine);
            return wrappedText.ToString();
        }

        internal class RendererOptions : TextOptions
        {
            public RendererOptions(Font font) : base(font)
            {
            }
        }
}