import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:uuid/uuid.dart';

import '../../../core/api/api_error.dart';
import '../../../core/format/formatters.dart';
import '../../../core/format/money_input.dart';
import '../data/transfers_repository.dart';
import '../domain/models.dart';

/// Transferência por chave (e-mail do destinatário): preencher → confirmar destinatário → enviar.
class TransferPage extends ConsumerStatefulWidget {
  const TransferPage({required this.sourceAccountId, super.key});

  final String sourceAccountId;

  @override
  ConsumerState<TransferPage> createState() => _TransferPageState();
}

class _TransferPageState extends ConsumerState<TransferPage> {
  final _email = TextEditingController();
  final _amount = TextEditingController();

  Recipient? _recipient;
  double? _value;

  /// Gerada ao confirmar o destinatário e reaproveitada em novas tentativas de envio:
  /// se a primeira chegou ao servidor, a repetição devolve a mesma transferência.
  String? _idempotencyKey;

  bool _busy = false;
  String? _error;

  @override
  void dispose() {
    _email.dispose();
    _amount.dispose();
    super.dispose();
  }

  Future<void> _review() async {
    final email = _email.text.trim();
    final value = parseMoney(_amount.text);

    if (!email.contains('@')) {
      return setState(() => _error = 'Informe o e-mail do destinatário.');
    }
    if (value == null) {
      return setState(() => _error = 'Informe um valor válido, com até duas casas decimais.');
    }

    await _run(() async {
      final recipient = await ref.read(transfersRepositoryProvider).findRecipient(email);
      if (recipient.accountId == widget.sourceAccountId) {
        throw ArgumentError('A conta de destino deve ser diferente da origem.');
      }
      setState(() {
        _recipient = recipient;
        _value = value;
        _idempotencyKey = const Uuid().v4();
      });
    });
  }

  Future<void> _confirm() async {
    final recipient = _recipient!;
    final value = _value!;

    await _run(() async {
      final transfer = await ref.read(transfersRepositoryProvider).create(
        sourceAccountId: widget.sourceAccountId,
        destinationAccountId: recipient.accountId,
        amount: value,
        idempotencyKey: _idempotencyKey!,
      );

      if (mounted) {
        context.pushReplacement(
          '/transfers/${transfer.id}?account=${widget.sourceAccountId}&amount=$value',
        );
      }
    });
  }

  void _edit() => setState(() {
    _recipient = null;
    _idempotencyKey = null;
    _error = null;
  });

  Future<void> _run(Future<void> Function() action) async {
    setState(() {
      _busy = true;
      _error = null;
    });

    try {
      await action();
    } on ArgumentError catch (error) {
      setState(() => _error = error.message as String);
    } on Object catch (error) {
      setState(() => _error = describeApiError(error));
    } finally {
      if (mounted) {
        setState(() => _busy = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final recipient = _recipient;

    return Scaffold(
      appBar: AppBar(title: const Text('Transferir')),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          if (recipient == null) ..._form() else ..._confirmation(recipient),
          if (_error != null)
            Padding(
              padding: const EdgeInsets.only(top: 16),
              child: Text(_error!, key: const Key('transfer-error'), style: TextStyle(color: Theme.of(context).colorScheme.error)),
            ),
        ],
      ),
    );
  }

  List<Widget> _form() => [
    TextField(
      key: const Key('recipient-email'),
      controller: _email,
      enabled: !_busy,
      keyboardType: TextInputType.emailAddress,
      autocorrect: false,
      decoration: const InputDecoration(
        labelText: 'E-mail do destinatário',
        helperText: 'Funciona como uma chave PIX',
        border: OutlineInputBorder(),
      ),
    ),
    const SizedBox(height: 16),
    TextField(
      key: const Key('amount'),
      controller: _amount,
      enabled: !_busy,
      keyboardType: const TextInputType.numberWithOptions(decimal: true),
      decoration: const InputDecoration(labelText: 'Valor', prefixText: r'R$ ', border: OutlineInputBorder()),
    ),
    const SizedBox(height: 24),
    FilledButton(onPressed: _busy ? null : _review, child: Text(_busy ? 'Buscando…' : 'Continuar')),
  ];

  List<Widget> _confirmation(Recipient recipient) {
    final theme = Theme.of(context);

    return [
      Card(
        child: Padding(
          padding: const EdgeInsets.all(20),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text('Você está enviando', style: theme.textTheme.labelLarge),
              Text(formatCurrency(_value!), style: theme.textTheme.headlineMedium),
              const SizedBox(height: 16),
              Text('Para', style: theme.textTheme.labelLarge),
              Text(recipient.displayName, key: const Key('recipient-name'), style: theme.textTheme.titleLarge),
              Text(recipient.email, style: theme.textTheme.bodySmall),
            ],
          ),
        ),
      ),
      const SizedBox(height: 24),
      FilledButton(onPressed: _busy ? null : _confirm, child: Text(_busy ? 'Enviando…' : 'Confirmar transferência')),
      const SizedBox(height: 8),
      TextButton(onPressed: _busy ? null : _edit, child: const Text('Alterar dados')),
    ];
  }
}
