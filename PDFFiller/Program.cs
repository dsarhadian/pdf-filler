using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
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
            var rootCommand = new RootCommand("Form processing application");

            // fill_form command
            var fillFormCommand = new Command("fill_form", "Fill a form using JSON data");

            var jsonFileOption = new Option<FileInfo>(
                "--json-file",
                "JSON file containing form data"
            );
            var pdfInputOption = new Option<FileInfo>("--pdf-input", "Input PDF file to fill");
            var pdfOutputOption = new Option<FileInfo>("--pdf-output", "Output PDF file to fill");

            fillFormCommand.AddOption(jsonFileOption);
            fillFormCommand.AddOption(pdfInputOption);
            fillFormCommand.AddOption(pdfOutputOption);

            fillFormCommand.SetHandler(
                async (FileInfo jsonFile, FileInfo pdfInput, FileInfo pdfOutput) =>
                {
                    await FillForm(jsonFile, pdfInput, pdfOutput);
                },
                jsonFileOption,
                pdfInputOption,
                pdfOutputOption
            );

            // extract_fields command
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

            rootCommand.AddCommand(fillFormCommand);
            rootCommand.AddCommand(extractFieldsCommand);

            return await rootCommand.InvokeAsync(args);
        }

        private static async Task FillForm(
            FileInfo jsonFile,
            FileInfo pdfInputFile,
            FileInfo pdfOutputFile
        )
        {
            if (jsonFile == null)
            {
                Console.WriteLine("Please provide a JSON file");
                return;
            }

            if (pdfInputFile == null)
            {
                Console.WriteLine("Please provide a PDF input file");
                return;
            }

            if (pdfOutputFile == null)
            {
                Console.WriteLine("Please provide a PDF output file");
                return;
            }

            pdfOutputFile.Directory.Create();

            try
            {
                string jsonContent = await File.ReadAllTextAsync(jsonFile.FullName);
                var formFields = JsonSerializer.Deserialize<List<FormField>>(jsonContent);

                PdfReader pdfReader = new PdfReader(pdfInputFile.FullName);
                PdfStamper pdfStamper = new PdfStamper(pdfReader, pdfOutputFile.Create());
                AcroFields acroFields = pdfStamper.AcroFields;

                foreach (var formField in formFields)
                {
                    if (formField.Type == FieldType.FIELD_TYPE_PUSHBUTTON)
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error ocurred while trying to file the PDF: {ex.Message}");
            }
        }

        private static void ExtractFields(FileInfo file)
        {
            if (file == null)
            {
                Console.WriteLine("Please provide a PDF file");
                return;
            }

            try
            {
                PdfReader pdfReader = new PdfReader(file.FullName);
                AcroFields acroFields = pdfReader.AcroFields;
                List<FormField> formFields = new List<FormField>();

                if (acroFields.Fields.Count == 0)
                {
                    Console.WriteLine("No form fields found in the PDF");
                    return;
                }

                foreach (var field in acroFields.Fields.Keys)
                {
                    string value = acroFields.GetField(field.ToString());
                    int typeInt = acroFields.GetFieldType(field.ToString());

                    var formField = new FormField
                    {
                        Name = field.ToString(),
                        Value = value,
                        Type = (FieldType)typeInt,
                    };

                    formFields.Add(formField);
                }

                var jsonOptions = new JsonSerializerOptions { WriteIndented = true };

                string jsonOutput = JsonSerializer.Serialize(formFields, jsonOptions);
                Console.WriteLine(jsonOutput);
            }
            catch (Exception eX)
            {
                Console.WriteLine(
                    $"An error ocurred while trying to run the extract_fields command: {eX.Message}"
                );
            }
        }
    }
}
