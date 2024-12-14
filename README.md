# PDFFiller

PDFFiller is a simple command line application that can fill out interactive
PDF forms.

## Built With

- .NET 8
- [iText](https://github.com/schourode/iTextSharp-LGPL)

## Usage

```bash
Description:
  Form processing application

Usage:
  PDFFiller [command] [options]

Options:
  --version       Show version information
  -?, -h, --help  Show help and usage information

Commands:
  fill_form       Fill a form using JSON data
  extract_fields  Extract fields from a PDF file
```

## Examples

Run `extract_fields` to get a JSON representation of the form fields inside the PDF

Edit the JSON and fill in any values.

Then run:

```bash
fill_form --json-file=test.json --pdf-input=form.pdf --pdf-output=out.pdf
```
