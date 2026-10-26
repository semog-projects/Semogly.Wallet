// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'transfer_status.dart';

part 'transfer_response.g.dart';

@JsonSerializable()
class TransferResponse {
  const TransferResponse({
    required this.id,
    required this.sourceAccountId,
    required this.destinationAccountId,
    required this.amount,
    required this.status,
    required this.failureReason,
    required this.createdAt,
    required this.updatedAt,
  });
  
  factory TransferResponse.fromJson(Map<String, Object?> json) => _$TransferResponseFromJson(json);
  
  final String id;
  final String sourceAccountId;
  final String destinationAccountId;
  final double amount;
  final TransferStatus status;
  final String? failureReason;
  final DateTime createdAt;
  final DateTime updatedAt;

  Map<String, Object?> toJson() => _$TransferResponseToJson(this);
}
