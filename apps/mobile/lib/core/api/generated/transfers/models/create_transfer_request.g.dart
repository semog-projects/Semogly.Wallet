// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'create_transfer_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

CreateTransferRequest _$CreateTransferRequestFromJson(
  Map<String, dynamic> json,
) => CreateTransferRequest(
  sourceAccountId: json['sourceAccountId'] as String,
  destinationAccountId: json['destinationAccountId'] as String,
  amount: (json['amount'] as num).toDouble(),
);

Map<String, dynamic> _$CreateTransferRequestToJson(
  CreateTransferRequest instance,
) => <String, dynamic>{
  'sourceAccountId': instance.sourceAccountId,
  'destinationAccountId': instance.destinationAccountId,
  'amount': instance.amount,
};
