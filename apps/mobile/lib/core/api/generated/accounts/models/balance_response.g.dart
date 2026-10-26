// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'balance_response.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

BalanceResponse _$BalanceResponseFromJson(Map<String, dynamic> json) =>
    BalanceResponse(
      accountId: json['accountId'] as String,
      balance: (json['balance'] as num).toDouble(),
      currency: json['currency'] as String,
    );

Map<String, dynamic> _$BalanceResponseToJson(BalanceResponse instance) =>
    <String, dynamic>{
      'accountId': instance.accountId,
      'balance': instance.balance,
      'currency': instance.currency,
    };
