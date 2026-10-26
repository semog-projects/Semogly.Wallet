// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'statement_entry_response.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

StatementEntryResponse _$StatementEntryResponseFromJson(
  Map<String, dynamic> json,
) => StatementEntryResponse(
  entryId: json['entryId'] as String,
  type: $enumDecode(_$EntryTypeEnumMap, json['type']),
  amount: (json['amount'] as num).toDouble(),
  balanceAfter: (json['balanceAfter'] as num).toDouble(),
  transferId: json['transferId'] as String?,
  description: json['description'] as String,
  occurredAt: DateTime.parse(json['occurredAt'] as String),
);

Map<String, dynamic> _$StatementEntryResponseToJson(
  StatementEntryResponse instance,
) => <String, dynamic>{
  'entryId': instance.entryId,
  'type': instance.type,
  'amount': instance.amount,
  'balanceAfter': instance.balanceAfter,
  'transferId': instance.transferId,
  'description': instance.description,
  'occurredAt': instance.occurredAt.toIso8601String(),
};

const _$EntryTypeEnumMap = {
  EntryType.debit: 'Debit',
  EntryType.credit: 'Credit',
};
