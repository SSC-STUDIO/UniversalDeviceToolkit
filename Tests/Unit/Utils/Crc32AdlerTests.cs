using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Utils;

[Trait("Category", TestCategories.Unit)]
public class Crc32AdlerTests
{
    #region Calculate(byte[]) Tests

    [Fact]
    public void Calculate_WithByteArray_ShouldReturnValidCrc()
    {
        // Arrange
        var data = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };

        // Act
        var crc = Crc32Adler.Calculate(data);

        // Assert
        crc.Should().NotBe(0);
    }

    [Fact]
    public void Calculate_WithEmptyByteArray_ShouldReturnValidCrc()
    {
        // Arrange
        var data = Array.Empty<byte>();

        // Act
        var crc = Crc32Adler.Calculate(data);

        // Assert - CRC of empty data should be 0xFFFFFFFF XORed, which equals 0x00000000
        crc.Should().Be(0x00000000);
    }

    [Fact]
    public void Calculate_WithSameData_ShouldReturnSameCrc()
    {
        // Arrange
        var data1 = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
        var data2 = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };

        // Act
        var crc1 = Crc32Adler.Calculate(data1);
        var crc2 = Crc32Adler.Calculate(data2);

        // Assert
        crc1.Should().Be(crc2);
    }

    [Fact]
    public void Calculate_WithDifferentData_ShouldReturnDifferentCrc()
    {
        // Arrange
        var data1 = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
        var data2 = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x06 };

        // Act
        var crc1 = Crc32Adler.Calculate(data1);
        var crc2 = Crc32Adler.Calculate(data2);

        // Assert
        crc1.Should().NotBe(crc2);
    }

    [Fact]
    public void Calculate_WithNullByteArray_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => Crc32Adler.Calculate((byte[])null!));
    }

    #endregion

    #region Calculate(byte[], int, int) Tests

    [Fact]
    public void Calculate_WithOffsetAndCount_ShouldReturnValidCrc()
    {
        // Arrange
        var data = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 };

        // Act
        var crc = Crc32Adler.Calculate(data, 1, 4);

        // Assert
        crc.Should().NotBe(0);
    }

    [Fact]
    public void Calculate_WithOffsetAndCount_ShouldCalculateCorrectSubset()
    {
        // Arrange
        var fullData = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
        var subsetData = new byte[] { 0x02, 0x03, 0x04 };

        // Act
        var fullCrc = Crc32Adler.Calculate(fullData);
        var subsetCrc = Crc32Adler.Calculate(fullData, 1, 3);
        var expectedCrc = Crc32Adler.Calculate(subsetData);

        // Assert
        subsetCrc.Should().Be(expectedCrc);
        subsetCrc.Should().NotBe(fullCrc);
    }

    [Fact]
    public void Calculate_WithZeroCount_ShouldReturnZero()
    {
        // Arrange
        var data = new byte[] { 0x01, 0x02, 0x03 };

        // Act
        var crc = Crc32Adler.Calculate(data, 0, 0);

        // Assert
        crc.Should().Be(0x00000000);
    }

    [Fact]
    public void Calculate_WithNullByteArrayAndOffset_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => Crc32Adler.Calculate((byte[])null!, 0, 0));
    }

    [Fact]
    public void Calculate_WithNegativeOffset_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var data = new byte[] { 0x01, 0x02, 0x03 };

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => Crc32Adler.Calculate(data, -1, 2));
    }

    [Fact]
    public void Calculate_WithNegativeCount_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var data = new byte[] { 0x01, 0x02, 0x03 };

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => Crc32Adler.Calculate(data, 0, -1));
    }

    [Fact]
    public void Calculate_WithOffsetCountExceedingBounds_ShouldThrowArgumentException()
    {
        // Arrange
        var data = new byte[] { 0x01, 0x02, 0x03 };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => Crc32Adler.Calculate(data, 0, 10));
    }

    [Fact]
    public void Calculate_WithOffsetAndCountAtEnd_ShouldCalculateCorrectly()
    {
        // Arrange
        var data = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
        var expectedData = new byte[] { 0x04, 0x05 };

        // Act
        var crc = Crc32Adler.Calculate(data, 3, 2);
        var expectedCrc = Crc32Adler.Calculate(expectedData);

        // Assert
        crc.Should().Be(expectedCrc);
    }

    #endregion

    #region Calculate(ReadOnlySpan<byte>) Tests

    [Fact]
    public void Calculate_WithSpan_ShouldReturnValidCrc()
    {
        // Arrange
        var data = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
        var span = new ReadOnlySpan<byte>(data);

        // Act
        var crc = Crc32Adler.Calculate(span);

        // Assert
        crc.Should().NotBe(0);
    }

    [Fact]
    public void Calculate_WithSpanAndArray_ShouldReturnSameResult()
    {
        // Arrange
        var data = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
        var span = new ReadOnlySpan<byte>(data);

        // Act
        var crcFromArray = Crc32Adler.Calculate(data);
        var crcFromSpan = Crc32Adler.Calculate(span);

        // Assert
        crcFromArray.Should().Be(crcFromSpan);
    }

    [Fact]
    public void Calculate_WithEmptySpan_ShouldReturnZero()
    {
        // Arrange
        var span = ReadOnlySpan<byte>.Empty;

        // Act
        var crc = Crc32Adler.Calculate(span);

        // Assert
        crc.Should().Be(0x00000000);
    }

    [Fact]
    public void Calculate_WithSpanSlice_ShouldReturnCorrectCrc()
    {
        // Arrange
        var data = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 };
        var span = new ReadOnlySpan<byte>(data);
        var slice = span.Slice(1, 4);
        var expectedData = new byte[] { 0x02, 0x03, 0x04, 0x05 };

        // Act
        var crcFromSlice = Crc32Adler.Calculate(slice);
        var expectedCrc = Crc32Adler.Calculate(expectedData);

        // Assert
        crcFromSlice.Should().Be(expectedCrc);
    }

    #endregion

    #region Calculate(IEnumerable<byte>) Tests

    [Fact]
    public void Calculate_WithEnumerable_ShouldReturnValidCrc()
    {
        // Arrange
        var data = new System.Collections.Generic.List<byte> { 0x01, 0x02, 0x03, 0x04, 0x05 };

        // Act
        var crc = Crc32Adler.Calculate(data);

        // Assert
        crc.Should().NotBe(0);
    }

    [Fact]
    public void Calculate_WithEnumerableAndArray_ShouldReturnSameResult()
    {
        // Arrange
        var arrayData = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
        var listData = new System.Collections.Generic.List<byte> { 0x01, 0x02, 0x03, 0x04, 0x05 };

        // Act
        var crcFromArray = Crc32Adler.Calculate(arrayData);
        var crcFromList = Crc32Adler.Calculate(listData);

        // Assert
        crcFromArray.Should().Be(crcFromList);
    }

    [Fact]
    public void Calculate_WithNullEnumerable_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => Crc32Adler.Calculate((System.Collections.Generic.IEnumerable<byte>)null!));
    }

    [Fact]
    public void Calculate_WithEmptyEnumerable_ShouldReturnZero()
    {
        // Arrange
        var data = new System.Collections.Generic.List<byte>();

        // Act
        var crc = Crc32Adler.Calculate(data);

        // Assert
        crc.Should().Be(0x00000000);
    }

    #endregion

    #region Known CRC32 Values Tests

    [Fact]
    public void Calculate_WithKnownData_ShouldReturnExpectedCrc()
    {
        // Arrange - "123456789" ASCII bytes
        var data = new byte[] { 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39 };

        // Act
        var crc = Crc32Adler.Calculate(data);

        // Assert - CRC32 of "123456789" is 0xCBF43926
        crc.Should().Be(0xCBF43926);
    }

    [Fact]
    public void Calculate_WithSingleByte_ShouldReturnCorrectCrc()
    {
        // Arrange
        var data = new byte[] { 0x00 };

        // Act
        var crc = Crc32Adler.Calculate(data);

        // Assert - CRC32 lookup at index 0xFF (0xFFFFFFFF XOR 0x00)
        crc.Should().Be(0xD202EF8D);
    }

    #endregion

    #region Large Data Tests

    [Fact]
    public void Calculate_WithLargeData_ShouldCalculateCorrectly()
    {
        // Arrange
        var data = new byte[1024];
        for (int i = 0; i < data.Length; i++)
            data[i] = (byte)(i % 256);

        // Act
        var crc = Crc32Adler.Calculate(data);

        // Assert
        crc.Should().NotBe(0);
    }

    [Fact]
    public void Calculate_WithSameLargeDataTwice_ShouldReturnSameCrc()
    {
        // Arrange
        var data1 = new byte[1024];
        var data2 = new byte[1024];
        for (int i = 0; i < 1024; i++)
        {
            data1[i] = (byte)(i % 256);
            data2[i] = (byte)(i % 256);
        }

        // Act
        var crc1 = Crc32Adler.Calculate(data1);
        var crc2 = Crc32Adler.Calculate(data2);

        // Assert
        crc1.Should().Be(crc2);
    }

    #endregion

    #region File path tests

    [Fact]
    public void Calculate_File_ShouldReturnConsistentChecksum()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "This is a test content for CRC32 calculation");

            var actualCrc = Crc32Adler.Calculate(tempFile);

            actualCrc.Should().BeGreaterThan(uint.MinValue);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void Calculate_NullFilePath_ShouldThrowArgumentNullException()
    {
        Action act = () => Crc32Adler.Calculate((string)null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Calculate_InvalidFile_ShouldThrowIOException()
    {
        Action act = () => Crc32Adler.Calculate("/path/to/non/existent/file.txt");

        act.Should().Throw<IOException>();
    }

    [Fact]
    public async Task Calculate_ShouldBeThreadSafe()
    {
        var testData = System.Text.Encoding.UTF8.GetBytes("This is a test for thread safety");

        var tasks = new Task<uint>[10];
        for (var i = 0; i < tasks.Length; i++)
            tasks[i] = Task.Run(() => Crc32Adler.Calculate(testData));

        var results = await Task.WhenAll(tasks);

        var firstResult = results[0];
        for (var i = 1; i < results.Length; i++)
            results[i].Should().Be(firstResult);
    }

    #endregion
}