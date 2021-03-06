﻿using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core.Models.EntityBase;
using Umbraco.Core.Models;
using Umbraco.Core.Models.Rdbms;
using Umbraco.Core.Persistence.Caching;
using Umbraco.Core.Persistence.Factories;
using Umbraco.Core.Persistence.Querying;
using Umbraco.Core.Persistence.Relators;
using Umbraco.Core.Persistence.UnitOfWork;

namespace Umbraco.Core.Persistence.Repositories
{
    /// <summary>
    /// Represents a repository for doing CRUD operations for <see cref="IMemberType"/>
    /// </summary>
    internal class MemberTypeRepository : ContentTypeBaseRepository<int, IMemberType>, IMemberTypeRepository
    {
         public MemberTypeRepository(IDatabaseUnitOfWork work)
            : base(work)
        {
        }

         public MemberTypeRepository(IDatabaseUnitOfWork work, IRepositoryCacheProvider cache)
            : base(work, cache)
        {
        }

         #region Overrides of RepositoryBase<int, IMemberType>

        protected override IMemberType PerformGet(int id)
        {
            var sql = GetBaseQuery(false);
            sql.Where(GetBaseWhereClause(), new { Id = id });
            sql.OrderByDescending<NodeDto>(x => x.NodeId);

            var dtos =
                Database.Fetch<MemberTypeReadOnlyDto, PropertyTypeReadOnlyDto, PropertyTypeGroupReadOnlyDto, MemberTypeReadOnlyDto>(
                    new PropertyTypePropertyGroupRelator().Map, sql);

             if (dtos == null || dtos.Any() == false)
                 return null;

             var factory = new MemberTypeReadOnlyFactory();
             var member = factory.BuildEntity(dtos.First());

             return member;
        }

        protected override IEnumerable<IMemberType> PerformGetAll(params int[] ids)
        {
            var sql = GetBaseQuery(false);
            if (ids.Any())
            {
                var statement = string.Join(" OR ", ids.Select(x => string.Format("umbracoNode.id='{0}'", x)));
                sql.Where(statement);
            }
            sql.OrderByDescending<NodeDto>(x => x.NodeId);

            var dtos =
                Database.Fetch<MemberTypeReadOnlyDto, PropertyTypeReadOnlyDto, PropertyTypeGroupReadOnlyDto, MemberTypeReadOnlyDto>(
                    new PropertyTypePropertyGroupRelator().Map, sql);

            return BuildFromDtos(dtos);
        }

        protected override IEnumerable<IMemberType> PerformGetByQuery(IQuery<IMemberType> query)
        {
            var sqlSubquery = GetSubquery();
            var translator = new SqlTranslator<IMemberType>(sqlSubquery, query);
            var subquery = translator.Translate();
            var sql = GetBaseQuery(false)
                .Append(new Sql("WHERE umbracoNode.id IN (" + subquery.SQL + ")", subquery.Arguments))
                .OrderBy<NodeDto>(x => x.SortOrder);

            var dtos =
                Database.Fetch<MemberTypeReadOnlyDto, PropertyTypeReadOnlyDto, PropertyTypeGroupReadOnlyDto, MemberTypeReadOnlyDto>(
                    new PropertyTypePropertyGroupRelator().Map, sql);

            return BuildFromDtos(dtos);
        }

        #endregion

        #region Overrides of PetaPocoRepositoryBase<int, IMemberType>

        protected override Sql GetBaseQuery(bool isCount)
        {
            var sql = new Sql();

            if (isCount)
            {
                sql.Select("COUNT(*)")
                    .From<NodeDto>()
                    .InnerJoin<ContentTypeDto>().On<ContentTypeDto, NodeDto>(left => left.NodeId, right => right.NodeId)
                    .Where<NodeDto>(x => x.NodeObjectType == NodeObjectTypeId);
                return sql;
            }

            sql.Select("umbracoNode.*", "cmsContentType.*", "cmsPropertyType.id AS PropertyTypeId", "cmsPropertyType.Alias", 
                "cmsPropertyType.Name", "cmsPropertyType.Description", "cmsPropertyType.helpText", "cmsPropertyType.mandatory",
                "cmsPropertyType.validationRegExp", "cmsPropertyType.dataTypeId", "cmsPropertyType.sortOrder AS PropertyTypeSortOrder",
                "cmsPropertyType.propertyTypeGroupId", "cmsMemberType.memberCanEdit", "cmsMemberType.viewOnProfile",
                "cmsDataType.controlId", "cmsDataType.dbType", "cmsPropertyTypeGroup.id AS PropertyTypeGroupId", 
                "cmsPropertyTypeGroup.text AS PropertyGroupName", "cmsPropertyTypeGroup.parentGroupId", 
                "cmsPropertyTypeGroup.sortorder AS PropertyGroupSortOrder")
                .From<NodeDto>()
                .InnerJoin<ContentTypeDto>().On<ContentTypeDto, NodeDto>(left => left.NodeId, right => right.NodeId)
                .LeftJoin<PropertyTypeDto>().On<PropertyTypeDto, NodeDto>(left => left.ContentTypeId, right => right.NodeId)
                .InnerJoin<MemberTypeDto>().On<MemberTypeDto, PropertyTypeDto>(left => left.PropertyTypeId, right => right.Id)
                .LeftJoin<DataTypeDto>().On<DataTypeDto, PropertyTypeDto>(left => left.DataTypeId, right => right.DataTypeId)
                .LeftJoin<PropertyTypeGroupDto>().On<PropertyTypeGroupDto, NodeDto>(left => left.ContentTypeNodeId, right => right.NodeId)
                .Where<NodeDto>(x => x.NodeObjectType == NodeObjectTypeId);
            return sql;
        }

        protected Sql GetSubquery()
        {
            var sql = new Sql()
                .Select("DISTINCT(umbracoNode.id)")
                .From<NodeDto>()
                .InnerJoin<ContentTypeDto>().On<ContentTypeDto, NodeDto>(left => left.NodeId, right => right.NodeId)
                .LeftJoin<PropertyTypeDto>().On<PropertyTypeDto, NodeDto>(left => left.ContentTypeId, right => right.NodeId)
                .InnerJoin<MemberTypeDto>().On<MemberTypeDto, PropertyTypeDto>(left => left.PropertyTypeId, right => right.Id)
                .LeftJoin<DataTypeDto>().On<DataTypeDto, PropertyTypeDto>(left => left.DataTypeId, right => right.DataTypeId)
                .LeftJoin<PropertyTypeGroupDto>().On<PropertyTypeGroupDto, NodeDto>(left => left.ContentTypeNodeId, right => right.NodeId)
                .Where<NodeDto>(x => x.NodeObjectType == NodeObjectTypeId);
            return sql;
        }

        protected override string GetBaseWhereClause()
        {
            return "umbracoNode.id = @Id";
        }

        protected override IEnumerable<string> GetDeleteClauses()
        {
            var list = new List<string>
                           {
                               "DELETE FROM umbracoUser2NodeNotify WHERE nodeId = @Id",
                               "DELETE FROM umbracoUser2NodePermission WHERE nodeId = @Id",
                               "DELETE FROM cmsTagRelationship WHERE nodeId = @Id",
                               "DELETE FROM cmsContentTypeAllowedContentType WHERE Id = @Id",
                               "DELETE FROM cmsContentTypeAllowedContentType WHERE AllowedId = @Id",
                               "DELETE FROM cmsContentType2ContentType WHERE parentContentTypeId = @Id",
                               "DELETE FROM cmsContentType2ContentType WHERE childContentTypeId = @Id",
                               "DELETE FROM cmsPropertyType WHERE contentTypeId = @Id",
                               "DELETE FROM cmsPropertyTypeGroup WHERE contenttypeNodeId = @Id",
                               "DELETE FROM cmsMemberType WHERE NodeId = @Id",
                               "DELETE FROM cmsContentType WHERE NodeId = @Id",
                               "DELETE FROM umbracoNode WHERE id = @Id"
                           };
            return list;
        }

        protected override Guid NodeObjectTypeId
        {
            get { return new Guid(Constants.ObjectTypes.MemberType); }
        }

        #endregion

        #region Unit of Work Implementation

        protected override void PersistNewItem(IMemberType entity)
        {
            ((MemberType)entity).AddingEntity();

            //By Convention we add 9 stnd PropertyTypes to an Umbraco MemberType
            var standardPropertyTypes = Constants.Conventions.Member.StandardPropertyTypeStubs;
            foreach (var standardPropertyType in standardPropertyTypes)
            {
                entity.AddPropertyType(standardPropertyType.Value);
            }

            var factory = new MemberTypeFactory(NodeObjectTypeId);
            var dto = factory.BuildDto(entity);

            PersistNewBaseContentType(dto, entity);

            //Handles the MemberTypeDto (cmsMemberType table)
            var memberTypeDtos = factory.BuildMemberTypeDtos(entity);
            foreach (var memberTypeDto in memberTypeDtos)
            {
                Database.Insert(memberTypeDto);
            }

            ((ICanBeDirty)entity).ResetDirtyProperties();
        }

        protected override void PersistUpdatedItem(IMemberType entity)
        {
            //Updates Modified date
            ((MemberType)entity).UpdatingEntity();

            //Look up parent to get and set the correct Path if ParentId has changed
            if (((ICanBeDirty)entity).IsPropertyDirty("ParentId"))
            {
                var parent = Database.First<NodeDto>("WHERE id = @ParentId", new { ParentId = entity.ParentId });
                entity.Path = string.Concat(parent.Path, ",", entity.Id);
                entity.Level = parent.Level + 1;
                var maxSortOrder =
                    Database.ExecuteScalar<int>(
                        "SELECT coalesce(max(sortOrder),0) FROM umbracoNode WHERE parentid = @ParentId AND nodeObjectType = @NodeObjectType",
                        new { ParentId = entity.ParentId, NodeObjectType = NodeObjectTypeId });
                entity.SortOrder = maxSortOrder + 1;
            }

            var factory = new MemberTypeFactory(NodeObjectTypeId);
            var dto = factory.BuildDto(entity);

            PersistUpdatedBaseContentType(dto, entity);

            //Remove existing entries before inserting new ones
            Database.Delete<MemberTypeDto>("WHERE NodeId = @Id", new {Id = entity.Id});

            //Handles the MemberTypeDto (cmsMemberType table)
            var memberTypeDtos = factory.BuildMemberTypeDtos(entity);
            foreach (var memberTypeDto in memberTypeDtos)
            {
                Database.Insert(memberTypeDto);
            }

            ((ICanBeDirty)entity).ResetDirtyProperties();
        }

        #endregion

        private IEnumerable<IMemberType> BuildFromDtos(List<MemberTypeReadOnlyDto> dtos)
        {
            if (dtos == null || dtos.Any() == false)
                return Enumerable.Empty<IMemberType>();

            var factory = new MemberTypeReadOnlyFactory();
            return dtos.Select(factory.BuildEntity);
        }
    }
}