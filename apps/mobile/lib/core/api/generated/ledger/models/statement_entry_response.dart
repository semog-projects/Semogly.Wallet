// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'entry_type.dart';

part 'statement_entry_response.g.dart';

@JsonSerializable()
class StatementEntryResponse {
  const StatementEntryResponse({
    required this.entryId,
    required this.type,
    required this.amount,
    required this.balanceAfter,
    required this.transferId,
    required this.description,
    required this.occurredAt,
  });
  
  factory StatementEntryResponse.fromJson(Map<String, Object?> json) => _$StatementEntryResponseFromJson(json);
  
  final String entryId;
  final EntryType type;
  final double amount;
  final double balanceAfter;
  final String? transferId;
  final String description;
  final DateTime occurredAt;

  Map<String, Object?> toJson() => _$StatementEntryResponseToJson(this);
}
