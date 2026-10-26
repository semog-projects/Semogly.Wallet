import 'package:dio/dio.dart';

/// Mensagem amigável para falhas da API (ProblemDetails do backend, rate limit do Gateway e rede).
String describeApiError(Object error) {
  if (error is! DioException) {
    return 'Algo deu errado. Tente novamente.';
  }

  if (error.type case DioExceptionType.connectionError || DioExceptionType.connectionTimeout) {
    return 'Sem conexão com o servidor. Verifique sua internet e tente novamente.';
  }

  final response = error.response;
  if (response?.statusCode == 429) {
    final retryAfter = int.tryParse(response?.headers.value('retry-after') ?? '');
    return retryAfter == null
        ? 'Muitas tentativas seguidas. Aguarde alguns segundos.'
        : 'Muitas tentativas seguidas. Tente novamente em ${retryAfter}s.';
  }

  // ProblemDetails: o backend explica o motivo em "detail"
  final data = response?.data;
  if (data is Map<String, dynamic> && data['detail'] is String) {
    return data['detail'] as String;
  }

  return switch (response?.statusCode) {
    404 => 'Não encontrado.',
    final status? when status >= 500 => 'O serviço está indisponível no momento. Tente mais tarde.',
    _ => 'Não foi possível concluir a operação.',
  };
}
