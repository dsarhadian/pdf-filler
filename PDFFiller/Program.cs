using System.CommandLine;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace PDFFiller
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var rootCommand = new RootCommand("Form processing application");

            // Create the fill_form command
            var fillFormCommand = new Command("fill_form", "Fill a form using JSON data");

            var jsonFileOption = new Option<string>("--json", "JSON file containing form data");
            var pdfInputOption = new Option<string>("--pdf", "Input PDF file to fill");
            var pdfFileInput = new Option<FileInfo>("--pdf-file", "PDF file to fill");

            fillFormCommand.AddOption(jsonFileOption);
            fillFormCommand.AddOption(pdfInputOption);
            fillFormCommand.AddOption(pdfFileInput);

            fillFormCommand.SetHandler(
                (string jsonData, string pdfData, FileInfo pdfFile) =>
                {
                    FillForm(jsonData, pdfData, pdfFile);
                },
                jsonFileOption,
                pdfInputOption,
                pdfFileInput
            );

            // Create the extract_fields command
            var extractFieldsCommand = new Command(
                "extract_fields",
                "Extract fields from a PDF file"
            );
            var extractFieldsOption = new Option<FileInfo>(
                "--pdf-file",
                "PDF File to extract data from"
            );

            extractFieldsCommand.AddOption(extractFieldsOption);

            extractFieldsCommand.SetHandler(
                (FileInfo pdfFile) =>
                {
                    ExtractFields(pdfFile);
                },
                extractFieldsOption
            );

            // Add commmands to rootCommand
            rootCommand.AddCommand(fillFormCommand);
            rootCommand.AddCommand(extractFieldsCommand);

            return rootCommand.Invoke(args);
        }

        private static int FillForm(string jsonData, string pdfData, FileInfo pdfFile)
        {
            if (string.IsNullOrEmpty(jsonData))
            {
                Console.Error.WriteLine("Please provide JSON data");
                Console.Error.Close();
                return 1;
            }

            if (string.IsNullOrEmpty(pdfData) && pdfFile == null)
            {
                Console.Error.WriteLine("Please provide PDF input data");
                Console.Error.Close();
                return 1;
            }

            try
            {
                var jsonDeserializeOptions = new JsonSerializerOptions()
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                };
                var formFields = JsonSerializer.Deserialize<List<FormField>>(
                    jsonData,
                    jsonDeserializeOptions
                );

                MemoryStream? inputPdfStream = null;
                if (!string.IsNullOrEmpty(pdfData))
                {
                    byte[] pdfBytes = Convert.FromBase64String(pdfData);
                    inputPdfStream = new MemoryStream(pdfBytes);
                }

                if (pdfFile != null)
                {
                    using (FileStream fileStream = File.OpenRead(pdfFile.FullName))
                    {
                        inputPdfStream = new MemoryStream();
                        inputPdfStream.SetLength(fileStream.Length);
                        fileStream.Read(inputPdfStream.GetBuffer(), 0, (int)fileStream.Length);
                    }
                }

                if (inputPdfStream == null)
                {
                    Console.Error.WriteLine("Could not create inputPdfStream");
                    Console.Error.Close();
                    return 1;
                }

                using (MemoryStream memoryStream = new MemoryStream())
                {
                    PdfReader pdfReader = new PdfReader(inputPdfStream);
                    PdfStamper pdfStamper = new PdfStamper(pdfReader, memoryStream);
                    AcroFields acroFields = pdfStamper.AcroFields;

                    if (formFields == null)
                    {
                        Console.Error.WriteLine("Error deserializing form fields");
                        Console.Error.Close();
                        return 1;
                    }

                    foreach (var formField in formFields)
                    {
                        int typeInt = acroFields.GetFieldType(formField.Name);

                        if ((FieldType)typeInt == FieldType.FIELD_TYPE_PUSHBUTTON)
                        {
                            Regex regex = new Regex(
                                @"^data:image/(?<mediaType>[^;]+);base64,(?<data>.*)"
                            );
                            Match match = regex.Match(formField.Value);

                            if (match.Length == 0)
                            {
                                acroFields.SetField(formField.Name, formField.Value);
                            }
                            else
                            {
                                // acroFields.SetField(formField.Name, match.Groups["data"].Value);
                                var fieldPosition = acroFields.GetFieldPositions(formField.Name);
                                Rectangle rect = new Rectangle(
                                    fieldPosition[1],
                                    fieldPosition[2],
                                    fieldPosition[3],
                                    fieldPosition[4]
                                );
                                Image image = Image.GetInstance(
                                    Convert.FromBase64String(match.Groups["data"].Value)
                                );

                                image.ScaleToFit(rect.Width, 65);

                                image.SetAbsolutePosition(rect.Left, (rect.Bottom - rect.Height));
                                pdfStamper.GetOverContent((int)fieldPosition[0]).AddImage(image);
                            }
                        }
                        else
                        {
                            acroFields.SetField(formField.Name, formField.Value);
                        }
                    }

                    pdfStamper.FormFlattening = true;
                    pdfStamper.Close();

                    byte[] pdfOutput = memoryStream.ToArray();

                    using var stdout = Console.OpenStandardOutput();
                    stdout.Write(pdfOutput);
                    stdout.Flush();
                }

                inputPdfStream?.Dispose();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"An error ocurred while trying to file the PDF: {ex.Message}"
                );
                Console.Error.Close();

                return 1;
            }

            return 0;
        }

        private static int ExtractFields(FileInfo file)
        {
            if (file == null)
            {
                Console.WriteLine("Please provide a PDF file");
                return 1;
            }

            try
            {
                PdfReader pdfReader = new PdfReader(file.FullName);
                AcroFields acroFields = pdfReader.AcroFields;
                List<FormField> formFields = new List<FormField>();

                if (acroFields.Fields.Count == 0)
                {
                    Console.WriteLine("No form fields found in the PDF");
                    return 1;
                }

                foreach (var field in acroFields.Fields.Keys)
                {
                    string value = acroFields.GetField(field.ToString());
                    int typeInt = acroFields.GetFieldType(field.ToString());
                    float[] position = acroFields.GetFieldPositions(field.ToString());
                    var left = position[1];
                    var right = position[3];
                    var top = position[4];
                    var bottom = position[2];
                    var acroField = acroFields.GetFieldItem(field.ToString());
                    var tabOrder = acroField.GetTabOrder(0);

                    var formField = new FormField
                    {
                        Name = field.ToString(),
                        Value = value,
                        Type = (FieldType)typeInt,
                        PageNumber = position[0],
                        Left = left,
                        Right = right,
                        Top = top,
                        Bottom = bottom,
                        TabOrder = tabOrder + 1,
                    };

                    formFields.Add(formField);
                }

                formFields = formFields.OrderBy(o => o.TabOrder).ToList();

                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                };

                string jsonOutput = JsonSerializer.Serialize(formFields, jsonOptions);
                Console.WriteLine(jsonOutput);
            }
            catch (Exception eX)
            {
                Console.WriteLine(
                    $"An error ocurred while trying to run the extract_fields command: {eX.Message}"
                );

                return 1;
            }

            return 0;
        }
    }
}
