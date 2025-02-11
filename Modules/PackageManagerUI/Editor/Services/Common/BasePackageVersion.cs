// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor.Scripting.ScriptCompilation;
using UnityEditor.Experimental.Licensing;

namespace UnityEditor.PackageManager.UI.Internal
{
    [Serializable]
    internal abstract class BasePackageVersion : IPackageVersion, ISerializationCallbackReceiver
    {
        [SerializeField]
        protected string m_Name;
        public string name => m_Name ?? string.Empty;

        [SerializeField]
        protected string m_DisplayName;
        public string displayName => m_DisplayName;

        [SerializeField]
        protected string m_Description;
        public string description => m_Description ?? string.Empty;

        public string packageUniqueId => m_Package?.uniqueId ?? string.Empty;

        [SerializeField]
        protected string m_VersionString;
        protected SemVersion? m_Version;
        public SemVersion? version => m_Version;

        public virtual string versionInManifest => null;

        [SerializeField]
        protected long m_PublishedDateTicks;
        public DateTime? publishedDate => m_PublishedDateTicks == 0 ? null : new DateTime(m_PublishedDateTicks, DateTimeKind.Utc);

        [SerializeField]
        protected string m_PublishNotes;
        public string localReleaseNotes => m_PublishNotes;

        public virtual DependencyInfo[] dependencies => null;
        public virtual DependencyInfo[] resolvedDependencies => null;
        public virtual EntitlementsInfo entitlements => null;

        public virtual bool isRegistryPackage => false;

        public virtual bool isFromScopedRegistry => false;

        [NonSerialized]
        private IPackage m_Package;
        public UI.IPackage package => m_Package;
        IPackage IPackageVersion.package
        {
            get => m_Package;
            set { m_Package = value; }
        }

        [SerializeField]
        protected PackageTag m_Tag;
        public bool HasTag(PackageTag tag)
        {
            return (m_Tag & tag) != 0;
        }

        public virtual RegistryType availableRegistry => RegistryType.None;

        // Currently we don't consider Upm Packages with EntitlementLicensingModel.AssetStore as having entitlements
        // and it is only used right now to check if a package is from Asset Store. This is also to be consistent with
        // other Asset Store packages (as in, if Upm Packages on Asset Store are considered with entitlements, then every
        // package from Asset Store should be considered to have entitlements).
        public bool hasEntitlements => entitlements != null &&
                (entitlements.licensingModel == EntitlementLicensingModel.Enterprise
                || entitlements.status == EntitlementStatus.NotGranted
                || entitlements.status == EntitlementStatus.Granted);

        public virtual bool hasEntitlementsError => false;

        public virtual IDictionary<string, string> categoryLinks => null;
        public virtual IEnumerable<UIError> errors => Enumerable.Empty<UIError>();
        public virtual IEnumerable<PackageSizeInfo> sizes => Enumerable.Empty<PackageSizeInfo>();
        public virtual IEnumerable<SemVersion> supportedVersions => Enumerable.Empty<SemVersion>();
        public virtual SemVersion? supportedVersion => null;

        public abstract string uniqueId { get; }
        public abstract string category { get; }
        public abstract string author { get; }
        public abstract bool isInstalled { get; }
        public abstract bool isFullyFetched { get; }
        public abstract bool isAvailableOnDisk { get; }
        public abstract bool isDirectDependency { get; }
        public abstract string localPath { get; }
        public abstract string versionString { get; }
        public abstract string versionId { get; }

        public bool IsDifferentVersionThanRequested
            => !string.IsNullOrEmpty(versionInManifest) && !HasTag(PackageTag.Git | PackageTag.Local | PackageTag.Custom) &&
                versionInManifest != versionString;

        public bool IsRequestedButOverriddenVersion
            => !string.IsNullOrEmpty(versionString) && !isInstalled &&
                versionString == m_Package?.versions.primary.versionInManifest;

        public virtual void OnBeforeSerialize()
        {
            // Do nothing
        }

        public virtual void OnAfterDeserialize()
        {
            SemVersionParser.TryParse(m_VersionString, out m_Version);
        }
    }
}
