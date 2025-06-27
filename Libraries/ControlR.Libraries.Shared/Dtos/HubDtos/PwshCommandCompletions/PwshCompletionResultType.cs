namespace ControlR.Libraries.Shared.Dtos.HubDtos.PwshCommandCompletions;

public enum PwshCompletionMatchType
{
  Text = 0,
  History = 1,
  Command = 2,
  ProviderItem = 3,
  ProviderContainer = 4,
  Property = 5,
  Method = 6,
  ParameterName = 7,
  ParameterValue = 8,
  Variable = 9,
  Namespace = 10,
  Type = 11,
  Keyword = 12,
  DynamicKeyword = 13,
}