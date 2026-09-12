using PayFlow.Application.DTOs;
using PayFlow.Application.Interfaces;

namespace PayFlow.Infrastructure.Parsers;

public class OpnCsvStatementParser : IStatementParser
{
    public string ProviderCode => "Opn";

    public IEnumerable<StatementRecordDto> ParseStatement(Stream fileStream, string fileName)
    {
        var records = CsvParserHelper.ReadCsvRecords(fileStream);
        if (records.Count < 2)
        {
            // Only header or empty file
            return [];
        }

        var headerMap = CsvParserHelper.BuildHeaderMap(records[0]);

        int idIdx = CsvParserHelper.GetColumnIndex(headerMap,
            "id", "chargeid", "transactionid", "providertransactionid", "transferid", "opntransactionid");
        int refIdx = CsvParserHelper.GetColumnIndex(headerMap,
            "merchantreference", "orderid", "metadataorderid", "description", "referenceno", "reference", "ref", "merchantref");
        int amountIdx = CsvParserHelper.GetColumnIndex(headerMap,
            "amount", "grossamount", "totalamount", "statementamount");
        int feeIdx = CsvParserHelper.GetColumnIndex(headerMap,
            "fee", "fees", "mdrfee", "chargefee", "feevat");
        int feeVatIdx = CsvParserHelper.GetColumnIndex(headerMap,
            "feevat", "vat");
        int netIdx = CsvParserHelper.GetColumnIndex(headerMap,
            "net", "netsettlement", "netamount", "settlementamount", "settledamount");
        int dateIdx = CsvParserHelper.GetColumnIndex(headerMap,
            "settlementdate", "paidat", "createdat", "date", "datetime", "transferdate", "timestamp");
        int statusIdx = CsvParserHelper.GetColumnIndex(headerMap,
            "status", "state", "paid", "result");

        // If feeIdx matches "feevat" because only "fee_vat" was present, do not duplicate
        bool separateFeeVat = feeVatIdx >= 0 && feeVatIdx != feeIdx;

        var result = new List<StatementRecordDto>();

        for (int i = 1; i < records.Count; i++)
        {
            var row = records[i];
            var providerTxnId = CsvParserHelper.GetFieldValue(row, idIdx);

            if (string.IsNullOrWhiteSpace(providerTxnId))
            {
                // Fallback: if idIdx wasn't found or empty, try column 0 if it looks like a txn id
                if (row.Count > 0 && !string.IsNullOrWhiteSpace(row[0]))
                {
                    providerTxnId = row[0].Trim();
                }
                else
                {
                    continue; // Skip invalid row
                }
            }

            var merchantRefRaw = CsvParserHelper.GetFieldValue(row, refIdx);
            string? merchantRef = string.IsNullOrWhiteSpace(merchantRefRaw) ? null : merchantRefRaw;

            var amount = CsvParserHelper.ParseDecimal(CsvParserHelper.GetFieldValue(row, amountIdx));
            var baseFee = CsvParserHelper.ParseDecimal(CsvParserHelper.GetFieldValue(row, feeIdx));
            var feeVat = separateFeeVat ? CsvParserHelper.ParseDecimal(CsvParserHelper.GetFieldValue(row, feeVatIdx)) : 0m;
            var totalFee = baseFee + feeVat;

            var netSettlement = netIdx >= 0
                ? CsvParserHelper.ParseDecimal(CsvParserHelper.GetFieldValue(row, netIdx))
                : 0m;

            if (netSettlement == 0m && amount > 0m)
            {
                netSettlement = amount - totalFee;
            }

            var settlementDate = CsvParserHelper.ParseDateTime(CsvParserHelper.GetFieldValue(row, dateIdx));

            var rawStatus = CsvParserHelper.GetFieldValue(row, statusIdx);
            var status = "SUCCESS";
            if (!string.IsNullOrWhiteSpace(rawStatus))
            {
                var normStatus = rawStatus.Trim().ToLowerInvariant();
                if (normStatus.Contains("fail") || normStatus.Contains("cancel") || normStatus == "expired")
                {
                    status = "FAILED";
                }
                else if (normStatus.Contains("refund"))
                {
                    status = "REFUNDED";
                }
                else
                {
                    status = "SUCCESS";
                }
            }

            result.Add(new StatementRecordDto
            {
                ProviderTransactionId = providerTxnId,
                MerchantReference = merchantRef,
                Amount = amount,
                Fee = totalFee,
                NetSettlement = netSettlement,
                SettlementDate = settlementDate,
                Status = status
            });
        }

        return result;
    }
}
