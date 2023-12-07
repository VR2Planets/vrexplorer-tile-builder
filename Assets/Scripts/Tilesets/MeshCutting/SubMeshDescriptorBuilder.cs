using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MeshCutting
{
    
    /// <summary>
    /// Facilitate the creation of Submeshes when building a new mesh.
    /// </summary>
    public class SubMeshDescriptorBuilder
    {
        private bool _requireReinit = true;
        private int _startingIndex = -1;
        private int _lastKnowSubmeshIndex = -1;
        private int _lastKnowHighestIndex;
        private List<SubMeshDescriptor> _list;
        private int _targetSubmeshCount;
        private readonly int _trianglesListLength;

        public SubMeshDescriptorBuilder(int subMeshCount, int trianglesListLength)
        {
            _targetSubmeshCount = subMeshCount;
            _trianglesListLength = trianglesListLength;
        }

        public void AddFace(int submeshIndex, int lowestIndex, int highestIndex)
        {
            if (_requireReinit)
            {
                _lastKnowSubmeshIndex = 0;
                _lastKnowHighestIndex = -1;
                _startingIndex = 0;
                _list = new List<SubMeshDescriptor>();
                _requireReinit = false;
            }

            // This is a while loop because we might have several empty submeshes before having actual content
            // order must remains to match source material list.
            while (_lastKnowSubmeshIndex < submeshIndex)
            {
                // append current submesh
                if (_startingIndex + _lastKnowHighestIndex - _startingIndex + 1 >= _trianglesListLength)
                {
                    Debug.LogWarning("Creating out of bounds submesh");
                }
                _list.Add(new SubMeshDescriptor(
                    _startingIndex,
                    _lastKnowHighestIndex - _startingIndex + 1,
                    MeshTopology.Triangles
                ));
                // go to next submesh
                _lastKnowSubmeshIndex++;
                // if _lastKnowSubmeshIndex is still lower that the current submeshIndex, there is another empty submesh
                if (_lastKnowSubmeshIndex < submeshIndex)
                {
                    _startingIndex = 0;
                    _lastKnowHighestIndex = -1;
                }
                else if (_lastKnowSubmeshIndex == submeshIndex)
                {
                    // we are starting new submesh at the current declared triangle
                    _startingIndex = lowestIndex;
                    _lastKnowHighestIndex = highestIndex;
                }
            }
            _startingIndex = Mathf.Min(lowestIndex, _startingIndex);
            _lastKnowHighestIndex = Mathf.Max(highestIndex, _lastKnowHighestIndex);
        }
        public List<SubMeshDescriptor> Complete()
        {
            if (_requireReinit || _list == null) return new List<SubMeshDescriptor>();
            _list.Add(new SubMeshDescriptor(
                _startingIndex,
                _lastKnowHighestIndex - _startingIndex + 1,
                MeshTopology.Triangles
            ));
            while (_list.Count < _targetSubmeshCount)
            {
                _list.Add(new SubMeshDescriptor(0, 0, MeshTopology.Triangles));
            }
            _requireReinit = true;
            return _list;
        }
    }
}