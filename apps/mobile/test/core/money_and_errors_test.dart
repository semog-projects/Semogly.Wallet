import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:semogly_wallet/core/api/api_error.dart';
import 'package:semogly_wallet/core/format/money_input.dart';

DioException responseError(int status, {Object? data, Map<String, List<String>> headers = const {}}) {
  final options = RequestOptions(path: '/transfers');
  return DioException(
    requestOptions: options,
    type: DioExceptionType.badResponse,
    response: Response(requestOptions: options, statusCode: status, data: data, headers: Headers.fromMap(headers)),
  );
}

void main() {
  group('parseMoney', () {
    for (final (raw, expected) in [('1.234,56', 1234.56), ('1234,5', 1234.5), ('1234.56', 1234.56), ('100', 100.0)]) {
      test('"$raw" → $expected', () => expect(parseMoney(raw), expected));
    }

    for (final raw in ['0', '-10', '1,234', 'abc', '2000000']) {
      test('recusa "$raw"', () => expect(parseMoney(raw), isNull));
    }
  });

  group('describeApiError', () {
    test('429 informa o tempo de espera do Retry-After', () {
      final error = responseError(429, headers: {'retry-after': ['6']});
      expect(describeApiError(error), 'Muitas tentativas seguidas. Tente novamente em 6s.');
    });

    test('usa o detail do ProblemDetails', () {
      final error = responseError(404, data: {'detail': 'Nenhuma conta ativa com este e-mail.'});
      expect(describeApiError(error), 'Nenhuma conta ativa com este e-mail.');
    });

    test('falha de conexão', () {
      final error = DioException(requestOptions: RequestOptions(), type: DioExceptionType.connectionError);
      expect(describeApiError(error), startsWith('Sem conexão'));
    });

    test('5xx sem detalhe', () {
      expect(describeApiError(responseError(503)), startsWith('O serviço está indisponível'));
    });
  });
}
