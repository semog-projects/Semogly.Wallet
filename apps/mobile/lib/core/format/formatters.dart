import 'package:intl/intl.dart';

final _currency = NumberFormat.currency(locale: 'pt_BR', symbol: r'R$');

// Padrão só numérico: dispensa carregar os dados de localização de datas
final _dateTime = DateFormat('dd/MM/yyyy HH:mm');

String formatCurrency(num value) => _currency.format(value);

String formatDateTime(DateTime value) => _dateTime.format(value.toLocal());
