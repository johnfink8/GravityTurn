/* 
The contents of this file is free and unencumbered software released into the public domain.

Anyone is free to copy, modify, publish, use, compile, sell, or
distribute this software, either in source code form or as a compiled
binary, for any purpose, commercial or non-commercial, and by any
means.

In jurisdictions that recognize copyright laws, the author or authors
of this software dedicate any and all copyright interest in the
software to the public domain. We make this dedication for the benefit
of the public at large and to the detriment of our heirs and
successors. We intend this dedication to be an overt act of
relinquishment in perpetuity of all present and future rights to this
software under copyright law.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
IN NO EVENT SHALL THE AUTHORS BE LIABLE FOR ANY CLAIM, DAMAGES OR
OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE,
ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
OTHER DEALINGS IN THE SOFTWARE.
*/

// This version of ReloadableMonoBehaviour is for a release version of your mod
// It disables the special stuff to make your plugin "reloadable".
// It should be copied into your project itself so your released mode
// has no requirement for the plugin reload extension.
#if !DEBUG
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace KramaxReloadExtensions
{
    public class ReloadableMonoBehaviour : MonoBehaviour
    {
        public MonoBehaviour AddComponentToObject(Type type, GameObject aGameObject)
        {
            var result = aGameObject.AddComponent(type) as MonoBehaviour;
            return result;
        }

        public MonoBehaviour AddComponent(Type type)
        {
            return this.AddComponentToObject(type, this.gameObject);
        }
    }
}
#endif
