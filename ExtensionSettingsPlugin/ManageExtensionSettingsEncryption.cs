using System;
using Microsoft.Xrm.Sdk;
using CCLLC.Xrm.Sdk;
using CCLLC.Xrm.Sdk.Utilities;

namespace CCLLCExtensionSettings
{
    [CrmPluginRegistration("Create", 
    "ccllc_extensionsettings", StageEnum.PreOperation, ExecutionModeEnum.Synchronous,
    "","Manage Extension Setting Encryption on Create", 1, 
    IsolationModeEnum.Sandbox 
    ,Id = "708cf666-526b-e811-a95b-000d3a33e0b1" 
    )]
    [CrmPluginRegistration("Update", 
    "ccllc_extensionsettings", StageEnum.PreOperation, ExecutionModeEnum.Synchronous,
    "ccllc_encryptedflag,ccllc_value","Manage Extension Setting Encryption on Update", 1, 
    IsolationModeEnum.Sandbox 
    ,Image1Type = ImageTypeEnum.PreImage
    ,Image1Name = "PreImage"
    ,Image1Attributes = "ccllc_encryptedflag,ccllc_value"
    ,Id = "1b5ce67f-526b-e811-a95b-000d3a33e0b1" 
    )]
    public class ManageExtensionSettingsEncryption : PluginBase  //not using instrumentation.
    {
        /// <summary>
        /// Register event handlers when the plugin loads for the first time. The configuration information
        /// for the Extension Settings entity is contained in <see cref="IExtensionSettingsConfig"/> so we load
        /// that first to get the entity name.
        /// </summary>
        /// <param name="unsecureConfig"></param>
        /// <param name="secureConfig"></param>
        public ManageExtensionSettingsEncryption(string unsecureConfig, string secureConfig) : base(unsecureConfig, secureConfig)
        {
            // get the configuration settings for the extension settings. These will be default
            // unless values unless overriden in RegisterContainerServices.
            var settings = Container.Resolve<IExtensionSettingsConfig>();
            
            RegisterEventHandler(settings.EntityName, MessageNames.Create, ePluginStage.PreOperation, ManageValueEncryption);
            RegisterEventHandler(settings.EntityName, MessageNames.Update, ePluginStage.PreOperation, ManageValueEncryption);
        }

        /// <summary>
        /// Encrypts the value in the extention setting record if the encryption flag is set. Also
        /// prevents removing the encryption once it is applied.
        /// </summary>
        /// <param name="localContext"></param>
        private void ManageValueEncryption(ILocalPluginContext localContext)
        {            
            var settings = Container.Resolve<IExtensionSettingsConfig>();

            if (localContext.PluginExecutionContext.MessageName == MessageNames.Update)
            {
                //validate preimage configuration
                if (localContext.PreImage == null)
                {
                    throw new Exception("Missing required preimage");
                }

                if (!localContext.PreImage.Contains(settings.EncryptionColumn))
                {
                    throw new Exception("Preimage is missing required encryption column.");
                }

                if (!localContext.PreImage.Contains(settings.ValueColumn))
                {
                    throw new Exception("Preimage is missing required value column.");
                }

                //if the record is already encrypted and the user is attempting to set to
                //unencrypted then block the update
                bool currentEncryptionStatus = localContext.PreImage.GetAttributeValue<bool>(settings.EncryptionColumn);
                if (currentEncryptionStatus && localContext.TargetEntity.Contains(settings.EncryptionColumn))
                {
                    throw new InvalidPluginExecutionException("Encryption cannot be removed once it has been applied.");
                }
            }

            Entity workingCopy = new Entity(localContext.TargetEntity.LogicalName, localContext.TargetEntity.Id);
            workingCopy.MergeWith(localContext.TargetEntity);

            //merge in any attributes from preimage that are not in the target entity.
            if (localContext.PluginExecutionContext.MessageName == MessageNames.Update)
            {
                workingCopy.MergeWith(localContext.PreImage);
            }

            //if the encrypted flag is set then encrypt the value on the way into storage.
            bool? encrypt = workingCopy.GetAttributeValue<bool?>(settings.EncryptionColumn);
            if (encrypt.HasValue && encrypt.Value)
            {                
                var encryptor = Container.Resolve<IRijndaelEncryption>();
                localContext.TargetEntity[settings.ValueColumn] = encryptor.Encrypt(workingCopy[settings.ValueColumn] as string, settings.EncryptionKey);
            }
        }

        
    }
}
