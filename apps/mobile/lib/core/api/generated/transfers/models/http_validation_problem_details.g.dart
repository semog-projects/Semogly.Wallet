// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'http_validation_problem_details.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

HttpValidationProblemDetails _$HttpValidationProblemDetailsFromJson(
  Map<String, dynamic> json,
) => HttpValidationProblemDetails(
  type: json['type'] as String?,
  title: json['title'] as String?,
  status: (json['status'] as num?)?.toInt(),
  detail: json['detail'] as String?,
  instance: json['instance'] as String?,
  errors: (json['errors'] as Map<String, dynamic>?)?.map(
    (k, e) =>
        MapEntry(k, (e as List<dynamic>).map((e) => e as String).toList()),
  ),
);

Map<String, dynamic> _$HttpValidationProblemDetailsToJson(
  HttpValidationProblemDetails instance,
) => <String, dynamic>{
  'type': instance.type,
  'title': instance.title,
  'status': instance.status,
  'detail': instance.detail,
  'instance': instance.instance,
  'errors': instance.errors,
};
