using System.CommandLine;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using iTextSharp.text.pdf;
using PDFFiller.Models;

namespace PDFFiller
{
    class Program
    {
        static int Main(string[] args)
        {
            // while (!Debugger.IsAttached) Thread.Sleep(100);

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var rootCommand = new RootCommand("Form processing application");

            // Create the fill_form command
            var fillFormCommand = new Command("fill_form", "Fill a form using JSON data");

            var jsonFileOption = new Option<string>("--json", "JSON file containing form data");
            var pdfFileInput = new Option<FileInfo>("--pdf-file", "PDF file to fill");

            fillFormCommand.AddOption(jsonFileOption);
            fillFormCommand.AddOption(pdfFileInput);

            fillFormCommand.SetHandler(
                (string jsonData, FileInfo pdfFile) =>
                {
                    FillForm(jsonData, pdfFile);
                },
                jsonFileOption,
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

            // Create the UB04 Claim Form Command
            var createUB04Command = new Command("create_ub04", "Create a UB04 Claim Form");
            jsonFileOption = new Option<string>("--json", "JSON file containing claim data");

            createUB04Command.AddOption(jsonFileOption);

            createUB04Command.SetHandler(
                (string jsonData) =>
                {
                    CreateUB04(jsonData);
                },
                jsonFileOption
            );

            // Add commmands to rootCommand
            rootCommand.AddCommand(fillFormCommand);
            rootCommand.AddCommand(extractFieldsCommand);
            rootCommand.AddCommand(createUB04Command);

            return rootCommand.Invoke(args);
        }

        private static int FillForm(string jsonData, FileInfo pdfFile)
        {
            if (string.IsNullOrEmpty(jsonData))
            {
                Console.Error.WriteLine("Please provide JSON data");
                Console.Error.Close();
                return 1;
            }

            if (pdfFile == null)
            {
                Console.Error.WriteLine("Please provide PDF input data");
                Console.Error.Close();
                return 1;
            }

            FormFiller formFiller = new FormFiller(pdfFile, jsonData);
            return formFiller.FillForm() ? 0 : 1;
        }

        private static int CreateUB04(string jsonData)
        {
            if (string.IsNullOrEmpty(jsonData))
            {
                Console.Error.WriteLine("Please provide JSON data");
                Console.Error.Close();
                return 1;
            }

            UB04ClaimForm uB04Claim = new UB04ClaimForm(jsonData);
            return uB04Claim.FillForm() ? 0 : 1;
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
