/// Valor digitado pelo usuário: aceita "1.234,56", "1234,56" ou "1234.56".
/// Retorna null se não for um valor monetário positivo com até duas casas decimais.
double? parseMoney(String raw, {double max = 1000000}) {
  final trimmed = raw.trim();
  final normalized = trimmed.contains(',') ? trimmed.replaceAll('.', '').replaceAll(',', '.') : trimmed;

  if (!RegExp(r'^\d+(\.\d{1,2})?$').hasMatch(normalized)) {
    return null;
  }

  final value = double.parse(normalized);
  return value > 0 && value <= max ? value : null;
}
