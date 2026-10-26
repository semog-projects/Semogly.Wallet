// Modelos do app, desacoplados dos DTOs gerados a partir do OpenAPI.

final class Account {
  const Account({required this.id, required this.ownerName, required this.balance, required this.isActive});

  final String id;
  final String ownerName;

  /// Saldo projetado pelo Accounts (consistência eventual em relação ao Ledger).
  final double balance;
  final bool isActive;
}

enum EntryKind { debit, credit }

final class StatementEntry {
  const StatementEntry({
    required this.id,
    required this.kind,
    required this.amount,
    required this.balanceAfter,
    required this.description,
    required this.occurredAt,
  });

  final String id;
  final EntryKind kind;
  final double amount;
  final double balanceAfter;
  final String description;
  final DateTime occurredAt;
}

final class StatementPage {
  const StatementPage({required this.balance, required this.entries});

  /// Saldo atual segundo o Ledger (fonte da verdade).
  final double balance;
  final List<StatementEntry> entries;
}

final class DailyUsage {
  const DailyUsage({required this.used, required this.limit, required this.remaining});

  final double used;
  final double limit;
  final double remaining;

  double get usedFraction => limit == 0 ? 0 : (used / limit).clamp(0, 1);
}
