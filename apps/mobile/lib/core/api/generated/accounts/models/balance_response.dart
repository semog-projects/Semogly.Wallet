// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'balance_response.g.dart';

@JsonSerializable()
class BalanceResponse {
  const BalanceResponse({
    required this.accountId,
    required this.balance,
    required this.currency,
  });
  
  factory BalanceResponse.fromJson(Map<String, Object?> json) => _$BalanceResponseFromJson(json);
  
  final String accountId;
  final double balance;
  final String currency;

  Map<String, Object?> toJson() => _$BalanceResponseToJson(this);
}
