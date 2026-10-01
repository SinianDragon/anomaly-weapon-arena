using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AnomalyArena
{
    [TestFixture]
    public class ArtSetTest
    {
        private const string ArtSetPath = "Assets/AnomalyArena/Art/ArtSet.asset";

        private static ArtSet LoadArtSet() => AssetDatabase.LoadAssetAtPath<ArtSet>(ArtSetPath);

        [Test]
        public void TextureSlots_AllAssigned()
        {
            var sut = LoadArtSet();

            var missing = typeof(ArtSet).GetFields()
                .Where(f => f.FieldType == typeof(Texture2D) && (Texture2D)f.GetValue(sut) == null)
                .Select(f => f.Name);

            Assert.That(missing, Is.Empty);
        }

        [Test]
        public void ExplosionFrames_ThreeFramesAllAssigned()
        {
            var sut = LoadArtSet();

            Assert.That(sut.explosionFrames, Has.Length.EqualTo(3).And.All.Not.Null);
        }

        // Art.BillboardLayer scales by 'glow image size / base image size' and places it concentrically: this requires the same margin on every side
        [TestCase("enemySmall", "enemySmallWindup")]
        [TestCase("enemyLarge", "enemyLargeWindup")]
        public void WindupTexture_PaddedEquallyOnAllSides(string baseField, string windupField)
        {
            var sut = LoadArtSet();
            var baseTex = (Texture2D)typeof(ArtSet).GetField(baseField).GetValue(sut);
            var windup = (Texture2D)typeof(ArtSet).GetField(windupField).GetValue(sut);

            int padX = windup.width - baseTex.width;
            int padY = windup.height - baseTex.height;

            Assert.That(padX, Is.EqualTo(padY).And.GreaterThan(0));
        }

        // The chain and wall bricks are tiled, so they must be imported as Repeat; otherwise only one link / one tile shows
        [TestCase("Assets/AnomalyArena/Art/Textures/hook_mid.png")]
        [TestCase("Assets/AnomalyArena/Art/Textures/wall_brick.png")]
        [TestCase("Assets/AnomalyArena/Art/Textures/floor_sand_muted.png")]
        public void TiledTexture_ImportedWithRepeatWrap(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);

            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));
        }
    }
}
