// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'deposit_response.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

DepositResponse _$DepositResponseFromJson(Map<String, dynamic> json) =>
    DepositResponse(
      entryId: json['entryId'] as String,
      accountId: json['accountId'] as String,
      amount: (json['amount'] as num).toDouble(),
      balanceAfter: (json['balanceAfter'] as num).toDouble(),
      occurredAt: DateTime.parse(json['occurredAt'] as String),
    );

Map<String, dynamic> _$DepositResponseToJson(DepositResponse instance) =>
    <String, dynamic>{
      'entryId': instance.entryId,
      'accountId': instance.accountId,
      'amount': instance.amount,
      'balanceAfter': instance.balanceAfter,
      'occurredAt': instance.occurredAt.toIso8601String(),
    };
