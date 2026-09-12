using System.Text;
using PayFlow.Application.DTOs;
using PayFlow.Application.Interfaces;

namespace PayFlow.Infrastructure.Parsers;

public class AutoDetectStatementParser : IStatementParser
{
    private readonly OpnCsvStatementParser _opnParser;
    private readonly GbPrimePayCsvStatementParser _gbPrimePayParser;

    public string ProviderCode => "AutoDetect";

    public AutoDetectStatementParser(
        OpnCsvStatementParser? opnParser = null,
        GbPrimePayCsvStatementParser? gbPrimePayParser = null)
    {
        _opnParser = opnParser ?? new OpnCsvStatementParser();
        _gbPrimePayParser = gbPrimePayParser ?? new GbPrimePayCsvStatementParser();
    }

    public IEnumerable<StatementRecordDto> ParseStatement(Stream fileStream, string fileName)
    {
        var (parser, seekableStream) = ResolveParserWithStream(fileStream, fileName);
        return parser.ParseStatement(seekableStream, fileName);
    }

    public string DetectProvider(Stream fileStream, string fileName)
    {
        var (parser, _) = ResolveParserWithStream(fileStream, fileName);
        return parser.ProviderCode;
    }

    public IStatementParser ResolveParser(Stream fileStream, string fileName)
    {
        var (parser, _) = ResolveParserWithStream(fileStream, fileName);
        return parser;
    }

    private (IStatementParser Parser, Stream Stream) ResolveParserWithStream(Stream fileStream, string fileName)
    {
        Stream seekableStream = fileStream;
        if (!fileStream.CanSeek)
        {
            var ms = new MemoryStream();
            fileStream.CopyTo(ms);
            ms.Position = 0;
            seekableStream = ms;
        }
        else
        {
            seekableStream.Position = 0;
        }

        string? headerLine = null;
        string? firstDataLine = null;

        using (var reader = new StreamReader(seekableStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true))
        {
            while ((headerLine = reader.ReadLine()) != null)
            {
                if (!string.IsNullOrWhiteSpace(headerLine))
                {
                    break;
                }
            }

            while ((firstDataLine = reader.ReadLine()) != null)
            {
                if (!string.IsNullOrWhiteSpace(firstDataLine))
                {
                    break;
                }
            }
        }

        // Rewind stream for downstream parser
        seekableStream.Position = 0;

        if (string.IsNullOrWhiteSpace(headerLine))
        {
            throw new InvalidOperationException($"Statement file '{fileName}' is empty.");
        }

        var normalizedHeader = CsvParserHelper.NormalizeKey(headerLine);

        // 1. Check header markers for GB Prime Pay
        if (normalizedHeader.Contains("gbpreferenceno") ||
            normalizedHeader.Contains("gbprimepay") ||
            normalizedHeader.Contains("gbprime") ||
            normalizedHeader.Contains("resultcode") ||
            normalizedHeader.Contains("merchantdefined") ||
            (normalizedHeader.Contains("referenceno") && normalizedHeader.Contains("detail")) ||
            headerLine.Contains("GB Reference", StringComparison.OrdinalIgnoreCase) ||
            headerLine.Contains("GBP", StringComparison.OrdinalIgnoreCase))
        {
            return (_gbPrimePayParser, seekableStream);
        }

        // 2. Check header markers for Opn / Omise
        if (normalizedHeader.Contains("omise") ||
            normalizedHeader.Contains("opn") ||
            normalizedHeader.Contains("chargeid") ||
            normalizedHeader.Contains("feevat") ||
            normalizedHeader.Contains("paidat") ||
            normalizedHeader.Contains("transferid") ||
            normalizedHeader.Contains("metadataorderid") ||
            normalizedHeader.Contains("fundingamount") ||
            headerLine.Contains("Opn", StringComparison.OrdinalIgnoreCase) ||
            headerLine.Contains("Omise", StringComparison.OrdinalIgnoreCase))
        {
            return (_opnParser, seekableStream);
        }

        // 3. Check first data line transaction ID signatures
        if (!string.IsNullOrWhiteSpace(firstDataLine))
        {
            var firstDataTokens = firstDataLine.Split(',');
            var firstCell = firstDataTokens.Length > 0 ? firstDataTokens[0].Trim('"', ' ', '\t') : string.Empty;

            if (firstCell.StartsWith("chrg_", StringComparison.OrdinalIgnoreCase) ||
                firstCell.StartsWith("trxn_", StringComparison.OrdinalIgnoreCase) ||
                firstDataLine.Contains("chrg_opn_", StringComparison.OrdinalIgnoreCase))
            {
                return (_opnParser, seekableStream);
            }

            if (firstCell.StartsWith("GB_", StringComparison.OrdinalIgnoreCase) ||
                firstCell.StartsWith("GBP", StringComparison.OrdinalIgnoreCase) ||
                firstDataLine.Contains("GB_", StringComparison.OrdinalIgnoreCase))
            {
                return (_gbPrimePayParser, seekableStream);
            }
        }

        // 4. Check fileName hints as fallback
        var lowerFileName = fileName.ToLowerInvariant();
        if (lowerFileName.Contains("opn") || lowerFileName.Contains("omise"))
        {
            return (_opnParser, seekableStream);
        }

        if (lowerFileName.Contains("gbprime") || lowerFileName.Contains("gb_") || lowerFileName.Contains("gbpay"))
        {
            return (_gbPrimePayParser, seekableStream);
        }

        throw new InvalidOperationException(
            $"Unable to automatically detect statement format for file '{fileName}'. Header row: '{headerLine}'.");
    }
}
