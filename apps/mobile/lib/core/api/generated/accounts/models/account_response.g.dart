// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'account_response.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

AccountResponse _$AccountResponseFromJson(Map<String, dynamic> json) =>
    AccountResponse(
      id: json['id'] as String,
      ownerName: json['ownerName'] as String,
      email: json['email'] as String,
      currency: json['currency'] as String,
      balance: (json['balance'] as num).toDouble(),
      status: $enumDecode(_$AccountStatusEnumMap, json['status']),
      createdAt: DateTime.parse(json['createdAt'] as String),
    );

Map<String, dynamic> _$AccountResponseToJson(AccountResponse instance) =>
    <String, dynamic>{
      'id': instance.id,
      'ownerName': instance.ownerName,
      'email': instance.email,
      'currency': instance.currency,
      'balance': instance.balance,
      'status': instance.status,
      'createdAt': instance.createdAt.toIso8601String(),
    };

const _$AccountStatusEnumMap = {
  AccountStatus.active: 'Active',
  AccountStatus.blocked: 'Blocked',
  AccountStatus.closed: 'Closed',
};
