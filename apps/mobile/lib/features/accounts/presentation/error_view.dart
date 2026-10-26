import 'package:dio/dio.dart';
import 'package:flutter/material.dart';

/// Mensagem de erro amigável com ação de tentar de novo.
class ErrorView extends StatelessWidget {
  const ErrorView({required this.error, required this.onRetry, super.key});

  final Object error;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(Icons.cloud_off_rounded, size: 40, color: Theme.of(context).colorScheme.error),
            const SizedBox(height: 12),
            Text(messageFor(error), textAlign: TextAlign.center),
            const SizedBox(height: 12),
            OutlinedButton(onPressed: onRetry, child: const Text('Tentar de novo')),
          ],
        ),
      ),
    );
  }

  static String messageFor(Object error) => switch (error) {
    DioException(type: DioExceptionType.connectionError || DioExceptionType.connectionTimeout) =>
      'Sem conexão com o servidor.',
    DioException(response: Response(statusCode: 429)) => 'Muitas requisições. Aguarde alguns segundos.',
    _ => 'Não foi possível carregar os dados.',
  };
}
