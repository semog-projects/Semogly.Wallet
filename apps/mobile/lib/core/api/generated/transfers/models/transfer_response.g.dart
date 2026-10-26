// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'transfer_response.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

TransferResponse _$TransferResponseFromJson(Map<String, dynamic> json) =>
    TransferResponse(
      id: json['id'] as String,
      sourceAccountId: json['sourceAccountId'] as String,
      destinationAccountId: json['destinationAccountId'] as String,
      amount: (json['amount'] as num).toDouble(),
      status: $enumDecode(_$TransferStatusEnumMap, json['status']),
      failureReason: json['failureReason'] as String?,
      createdAt: DateTime.parse(json['createdAt'] as String),
      updatedAt: DateTime.parse(json['updatedAt'] as String),
    );

Map<String, dynamic> _$TransferResponseToJson(TransferResponse instance) =>
    <String, dynamic>{
      'id': instance.id,
      'sourceAccountId': instance.sourceAccountId,
      'destinationAccountId': instance.destinationAccountId,
      'amount': instance.amount,
      'status': instance.status,
      'failureReason': instance.failureReason,
      'createdAt': instance.createdAt.toIso8601String(),
      'updatedAt': instance.updatedAt.toIso8601String(),
    };

const _$TransferStatusEnumMap = {
  TransferStatus.pending: 'Pending',
  TransferStatus.completed: 'Completed',
  TransferStatus.failed: 'Failed',
};
