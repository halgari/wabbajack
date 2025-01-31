﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.CompilationSteps.CompilationErrors;
using Xunit;
using Xunit.Abstractions;
using File = Alphaleonis.Win32.Filesystem.File;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.Test
{
    public class SanityTests : ACompilerTest
    {

        public SanityTests(ITestOutputHelper helper) : base(helper)
        {
        }

        
        [Fact]
        public async Task TestDirectMatch() 
        {

            var profile = utils.AddProfile();
            var mod = utils.AddMod();
            var testPex = utils.AddModFile(mod, @"Data\scripts\test.pex", 10);

            await utils.Configure();

            utils.AddManualDownload(
                new Dictionary<string, byte[]> {{"/baz/biz.pex", await testPex.ReadAllBytesAsync()}});

            await CompileAndInstall(profile);

            utils.VerifyInstalledFile(mod, @"Data\scripts\test.pex");
        }
        
        [Fact]
        public async Task TestDirectMatchFromGameFolder()
        {

            var profile = utils.AddProfile();
            var mod = utils.AddMod();
            var testPex = utils.AddGameFile(@"enbstuff\test.pex", 10);

            await utils.Configure();

            utils.AddManualDownload(
                new Dictionary<string, byte[]> {{"/baz/biz.pex", await testPex.ReadAllBytesAsync()}});

            await CompileAndInstall(profile);

            utils.VerifyInstalledGameFile(@"enbstuff\test.pex");
        }
        
        [Fact]
        public async Task TestDirectMatchIsIgnoredWhenGameFolderFilesOverrideExists()
        {

            var profile = utils.AddProfile();
            var mod = utils.AddMod();
            var testPex = utils.AddGameFile(@"enbstuff\test.pex", 10);

            await utils.Configure();

            utils.MO2Folder.Combine(Consts.GameFolderFilesDir).CreateDirectory();

            utils.AddManualDownload(
                new Dictionary<string, byte[]> {{"/baz/biz.pex", await testPex.ReadAllBytesAsync()}});

            await CompileAndInstall(profile);

            Assert.False(utils.InstallFolder.Combine(Consts.GameFolderFilesDir, (RelativePath)@"enbstuff\test.pex").IsFile);
        }

        [Fact]
        public async Task TestDuplicateFilesAreCopied()
        {

            var profile = utils.AddProfile();
            var mod = utils.AddMod();
            var testPex = utils.AddModFile(mod, @"Data\scripts\test.pex", 10);

            // Make a copy to make sure it gets picked up and moved around.
            testPex.CopyTo(testPex.WithExtension(new Extension(".copy")));

            await utils.Configure();

            utils.AddManualDownload(
                new Dictionary<string, byte[]> { { "/baz/biz.pex", await testPex.ReadAllBytesAsync() } });

            await CompileAndInstall(profile);

            utils.VerifyInstalledFile(mod, @"Data\scripts\test.pex");
            utils.VerifyInstalledFile(mod, @"Data\scripts\test.pex.copy");
        }

        [Fact]
        public async Task TestUpdating()
        {

            var profile = utils.AddProfile();
            var mod = utils.AddMod();
            var unchanged = utils.AddModFile(mod, @"Data\scripts\unchanged.pex", 10);
            var deleted = utils.AddModFile(mod, @"Data\scripts\deleted.pex", 10);
            var modified = utils.AddModFile(mod, @"Data\scripts\modified.pex", 10);

            await utils.Configure();

            utils.AddManualDownload(
                new Dictionary<string, byte[]>
                {
                    { "/baz/unchanged.pex", await unchanged.ReadAllBytesAsync() },
                    { "/baz/deleted.pex", await deleted.ReadAllBytesAsync() },
                    { "/baz/modified.pex", await modified.ReadAllBytesAsync() },
                });

            await CompileAndInstall(profile);

            utils.VerifyInstalledFile(mod, @"Data\scripts\unchanged.pex");
            utils.VerifyInstalledFile(mod, @"Data\scripts\deleted.pex");
            utils.VerifyInstalledFile(mod, @"Data\scripts\modified.pex");

            var unchangedPath = utils.PathOfInstalledFile(mod, @"Data\scripts\unchanged.pex");
            var deletedPath = utils.PathOfInstalledFile(mod, @"Data\scripts\deleted.pex");
            var modifiedPath = utils.PathOfInstalledFile(mod, @"Data\scripts\modified.pex");

            var extraPath = utils.PathOfInstalledFile(mod, @"something_i_made.foo");
            await extraPath.WriteAllTextAsync("bleh");

            var extraFolder = utils.PathOfInstalledFile(mod, @"something_i_made.foo").Parent.Combine("folder_i_made");
            extraFolder.CreateDirectory();
            
            Assert.True(extraFolder.IsDirectory);


            var unchangedModified = unchangedPath.LastModified;

            await modifiedPath.WriteAllTextAsync("random data");
            var modifiedModified = modifiedPath.LastModified;

            deletedPath.Delete();

            Assert.True(extraPath.Exists);
            
            await CompileAndInstall(profile);

            utils.VerifyInstalledFile(mod, @"Data\scripts\unchanged.pex");
            utils.VerifyInstalledFile(mod, @"Data\scripts\deleted.pex");
            utils.VerifyInstalledFile(mod, @"Data\scripts\modified.pex");

            Assert.Equal(unchangedModified, unchangedPath.LastModified);
            Assert.NotEqual(modifiedModified, modifiedPath.LastModified);
            Assert.False(extraPath.Exists);
            Assert.False(extraFolder.Exists);
        }


        [Fact]
        public async Task CleanedESMTest()
        {
            var profile = utils.AddProfile();
            var mod = utils.AddMod("Cleaned ESMs");
            var updateEsm = utils.AddModFile(mod, @"Update.esm", 10);

            await utils.Configure();

            var gameFile = utils.GameFolder.Combine("Data", "Update.esm");
            utils.GenerateRandomFileData(gameFile, 20);

            var modlist = await CompileAndInstall(profile);

            utils.VerifyInstalledFile(mod, @"Update.esm");

            var compiler = await ConfigureAndRunCompiler(profile);

            // Update the file and verify that it throws an error.
            utils.GenerateRandomFileData(gameFile, 20);
            var exception = await Assert.ThrowsAsync<InvalidGameESMError>(async () => await Install(compiler));
            Assert.IsAssignableFrom<InvalidGameESMError>(exception);
        }

        [Fact]
        public async Task SetScreenSizeTest()
        {
            var profile = utils.AddProfile();
            var mod = utils.AddMod("dummy");

            await utils.Configure();
            await utils.MO2Folder.Combine("profiles", profile, "somegameprefs.ini").WriteAllLinesAsync(
                // Beth inis are messy, let's make ours just as messy to catch some parse failures
                "[Display]",
                "foo=4",
                "[Display]",
                "STestFile=f",
                "STestFile=",
                "[Display]",
                "foo=4",
                "iSize H=50", 
                "iSize W=100",
                "[MEMORY]",
                "VideoMemorySizeMb=22");

            var modlist = await CompileAndInstall(profile);

            var ini = utils.InstallFolder.Combine("profiles", profile, "somegameprefs.ini").LoadIniFile();

            var sysinfo = CreateDummySystemParameters();

            Assert.Equal(sysinfo.ScreenHeight.ToString(), ini?.Display?["iSize H"]);
            Assert.Equal(sysinfo.ScreenWidth.ToString(), ini?.Display?["iSize W"]);
            Assert.Equal(sysinfo.EnbLEVRAMSize.ToString(), ini?.MEMORY?["VideoMemorySizeMb"]);
        }

        [Fact]
        public async Task UnmodifiedInlinedFilesArePulledFromArchives()
        {
            var profile = utils.AddProfile();
            var mod = utils.AddMod();
            var ini = utils.AddModFile(mod, @"foo.ini", 10);
            await utils.Configure();

            utils.AddManualDownload(
                new Dictionary<string, byte[]> { { "/baz/biz.pex", await ini.ReadAllBytesAsync() } });

            var modlist = await CompileAndInstall(profile);
            var directive = modlist.Directives.FirstOrDefault(m => m.To == (RelativePath)$"mods\\{mod}\\foo.ini");

            Assert.NotNull(directive);
            Assert.IsAssignableFrom<FromArchive>(directive);
        }

        [Fact]
        public async Task ModifiedIniFilesArePatchedAgainstFileWithSameName()
        {
            var profile = utils.AddProfile();
            var mod = utils.AddMod();
            var ini = utils.AddModFile(mod, @"foo.ini", 10);
            var meta = utils.AddModFile(mod, "meta.ini");

            await utils.Configure();


            var archive = utils.AddManualDownload(
                new Dictionary<string, byte[]> { { "/baz/foo.ini", await ini.ReadAllBytesAsync() } });

            await meta.WriteAllLinesAsync(
                "[General]",
                $"installationFile={archive}");

            // Modify after creating mod archive in the downloads folder
            await ini.WriteAllTextAsync("Wabbajack, Wabbajack, Wabbajack!");

            var modlist = await CompileAndInstall(profile);
            var directive = modlist.Directives.FirstOrDefault(m => m.To == (RelativePath)$"mods\\{mod}\\foo.ini");

            Assert.NotNull(directive);
            Assert.IsAssignableFrom<PatchedFromArchive>(directive);
        }

    }
}
