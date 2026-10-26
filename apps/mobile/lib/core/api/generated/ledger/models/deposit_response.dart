// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'deposit_response.g.dart';

@JsonSerializable()
class DepositResponse {
  const DepositResponse({
    required this.entryId,
    required this.accountId,
    required this.amount,
    required this.balanceAfter,
    required this.occurredAt,
  });
  
  factory DepositResponse.fromJson(Map<String, Object?> json) => _$DepositResponseFromJson(json);
  
  final String entryId;
  final String accountId;
  final double amount;
  final double balanceAfter;
  final DateTime occurredAt;

  Map<String, Object?> toJson() => _$DepositResponseToJson(this);
}
