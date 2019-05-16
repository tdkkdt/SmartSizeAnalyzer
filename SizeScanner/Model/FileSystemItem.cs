using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using SizeScanner.Annotations;

namespace SizeScanner.Model {
    class FileSystemItem : IComparable<FileSystemItem> {
        public string Name { get; private set; }

        public long Size { get; set; }

        public bool IsValid { get; private set; }
        public SortedSet<FileSystemItem> InnerItems { get; }

        public static FileSystemItem CreateEmptyInvalid() {
            FileSystemItem result = new FileSystemItem("Empty");
            result.MakeInvalid();
            return result;
        }

        public FileSystemItem(string name) {
            Name = name;
            IsValid = true;
            InnerItems = new SortedSet<FileSystemItem>();
        }

        public FileSystemItem(string name, long size) : this(name) {
            Size = size;
        }

        public FileSystemItem(FileInfo fileInfo) : this(fileInfo.Name, fileInfo.Length) {
        }

        public void MakeInvalid() {
            IsValid = true;
            Size = 0;
        }

        public void RenameInner(FileSystemItem item, string newName) {
            InnerItems.Remove(item);
            item.Name = newName;
            InnerItems.Add(item);
        }

        public void RenameAsRoot(string newName) {
            Name = newName;
        }

        public int CompareTo(FileSystemItem other) {
            return string.Compare(Name, other.Name, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != GetType())
                return false;
            return Equals((FileSystemItem) obj);
        }

        bool Equals(FileSystemItem other) {
            return string.Equals(Name, other.Name);
        }

        public override int GetHashCode() {
            return (Name != null ? Name.GetHashCode() : 0);
        }
    }
}