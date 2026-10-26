// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'account_response.dart';

part 'paged_response_of_account_response.g.dart';

@JsonSerializable()
class PagedResponseOfAccountResponse {
  const PagedResponseOfAccountResponse({
    required this.items,
    required this.page,
    required this.pageSize,
    required this.totalCount,
    this.totalPages,
  });
  
  factory PagedResponseOfAccountResponse.fromJson(Map<String, Object?> json) => _$PagedResponseOfAccountResponseFromJson(json);
  
  final List<AccountResponse> items;
  final int page;
  final int pageSize;
  final int totalCount;
  final int? totalPages;

  Map<String, Object?> toJson() => _$PagedResponseOfAccountResponseToJson(this);
}
