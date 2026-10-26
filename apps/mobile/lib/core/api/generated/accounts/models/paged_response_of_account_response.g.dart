// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'paged_response_of_account_response.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

PagedResponseOfAccountResponse _$PagedResponseOfAccountResponseFromJson(
  Map<String, dynamic> json,
) => PagedResponseOfAccountResponse(
  items: (json['items'] as List<dynamic>)
      .map((e) => AccountResponse.fromJson(e as Map<String, dynamic>))
      .toList(),
  page: (json['page'] as num).toInt(),
  pageSize: (json['pageSize'] as num).toInt(),
  totalCount: (json['totalCount'] as num).toInt(),
  totalPages: (json['totalPages'] as num?)?.toInt(),
);

Map<String, dynamic> _$PagedResponseOfAccountResponseToJson(
  PagedResponseOfAccountResponse instance,
) => <String, dynamic>{
  'items': instance.items,
  'page': instance.page,
  'pageSize': instance.pageSize,
  'totalCount': instance.totalCount,
  'totalPages': instance.totalPages,
};
