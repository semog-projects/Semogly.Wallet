// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'statement_entry_response.dart';

part 'statement_response.g.dart';

@JsonSerializable()
class StatementResponse {
  const StatementResponse({
    required this.accountId,
    required this.balance,
    required this.page,
    required this.pageSize,
    required this.entries,
  });
  
  factory StatementResponse.fromJson(Map<String, Object?> json) => _$StatementResponseFromJson(json);
  
  final String accountId;
  final double balance;
  final int page;
  final int pageSize;
  final List<StatementEntryResponse> entries;

  Map<String, Object?> toJson() => _$StatementResponseToJson(this);
}
