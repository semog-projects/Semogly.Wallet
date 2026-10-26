// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'ledger_book_response.g.dart';

@JsonSerializable()
class LedgerBookResponse {
  const LedgerBookResponse({
    required this.cashIn,
    required this.clearing,
    required this.customerBalances,
    required this.customerAccounts,
    required this.total,
    this.isBalanced,
  });
  
  factory LedgerBookResponse.fromJson(Map<String, Object?> json) => _$LedgerBookResponseFromJson(json);
  
  final double cashIn;
  final double clearing;
  final double customerBalances;
  final int customerAccounts;
  final double total;
  final bool? isBalanced;

  Map<String, Object?> toJson() => _$LedgerBookResponseToJson(this);
}
