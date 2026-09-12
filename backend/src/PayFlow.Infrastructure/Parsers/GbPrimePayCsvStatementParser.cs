using PayFlow.Application.DTOs;
using PayFlow.Application.Interfaces;

namespace PayFlow.Infrastructure.Parsers;

public class GbPrimePayCsvStatementParser : IStatementParser
{
    public string ProviderCode => "GBPrimePay";

    public IEnumerable<StatementRecordDto> ParseStatement(Stream fileStream, string fileName)
    {
        var records = CsvParserHelper.ReadCsvRecords(fileStream);
        if (records.Count < 2)
        {
            return [];
        }

        var headerMap = CsvParserHelper.BuildHeaderMap(records[0]);

        int idIdx = CsvParserHelper.GetColumnIndex(headerMap,
            "gbpreferenceno", "referenceno", "transactionid", "transactionno", "providertransactionid", "gbtransactionid", "refno", "id");
        int refIdx = CsvParserHelper.GetColumnIndex(headerMap,
            "detail", "merchantdefined1", "merchantdefined", "merchantreference", "merchantorderid", "orderid", "customerreference", "reference", "ref");
        int amountIdx = CsvParserHelper.GetColumnIndex(headerMap,
            "amount", "totalamount", "grossamount", "statementamount");
        int feeIdx = CsvParserHelper.GetColumnIndex(headerMap,
            "fee", "chargefee", "totalfee", "servicefee", "mdr");
        int vatIdx = CsvParserHelper.GetColumnIndex(headerMap,
            "vat", "feevat");
        int netIdx = CsvParserHelper.GetColumnIndex(headerMap,
            "netamount", "settlementamount", "netsettlement", "net", "settledamount");
        int dateIdx = CsvParserHelper.GetColumnIndex(headerMap,
            "date", "paymentdate", "datetime", "settlementdate", "createddate", "transactiondate", "timestamp");
        int resultCodeIdx = CsvParserHelper.GetColumnIndex(headerMap,
            "resultcode");
        int statusIdx = CsvParserHelper.GetColumnIndex(headerMap,
            "status", "state", "result");

        bool separateVat = vatIdx >= 0 && vatIdx != feeIdx;

        var result = new List<StatementRecordDto>();

        for (int i = 1; i < records.Count; i++)
        {
            var row = records[i];
            var providerTxnId = CsvParserHelper.GetFieldValue(row, idIdx);

            if (string.IsNullOrWhiteSpace(providerTxnId))
            {
                if (row.Count > 0 && !string.IsNullOrWhiteSpace(row[0]))
                {
                    providerTxnId = row[0].Trim();
                }
                else
                {
                    continue;
                }
            }

            var merchantRefRaw = CsvParserHelper.GetFieldValue(row, refIdx);
            string? merchantRef = string.IsNullOrWhiteSpace(merchantRefRaw) ? null : merchantRefRaw;

            var amount = CsvParserHelper.ParseDecimal(CsvParserHelper.GetFieldValue(row, amountIdx));
            var baseFee = CsvParserHelper.ParseDecimal(CsvParserHelper.GetFieldValue(row, feeIdx));
            var vat = separateVat ? CsvParserHelper.ParseDecimal(CsvParserHelper.GetFieldValue(row, vatIdx)) : 0m;
            var totalFee = baseFee + vat;

            var netSettlement = netIdx >= 0
                ? CsvParserHelper.ParseDecimal(CsvParserHelper.GetFieldValue(row, netIdx))
                : 0m;

            if (netSettlement == 0m && amount > 0m)
            {
                netSettlement = amount - totalFee;
            }

            var settlementDate = CsvParserHelper.ParseDateTime(CsvParserHelper.GetFieldValue(row, dateIdx));

            var resultCode = CsvParserHelper.GetFieldValue(row, resultCodeIdx);
            var rawStatus = CsvParserHelper.GetFieldValue(row, statusIdx);

            var status = "SUCCESS";
            if (!string.IsNullOrWhiteSpace(resultCode))
            {
                status = resultCode.Trim() == "00" ? "SUCCESS" : "FAILED";
            }
            else if (!string.IsNullOrWhiteSpace(rawStatus))
            {
                var norm = rawStatus.Trim().ToLowerInvariant();
                if (norm.Contains("fail") || norm.Contains("cancel") || norm.Contains("declined") || norm == "error")
                {
                    status = "FAILED";
                }
                else if (norm.Contains("refund"))
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
