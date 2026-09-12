using PayFlow.Application.DTOs;

namespace PayFlow.Application.Interfaces;

public interface IStatementParser
{
    string ProviderCode { get; }
    IEnumerable<StatementRecordDto> ParseStatement(Stream fileStream, string fileName);
}
