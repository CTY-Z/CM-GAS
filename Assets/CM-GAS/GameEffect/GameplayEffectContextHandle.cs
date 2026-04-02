using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CMGAS
{
	public class GameplayEffectContextHandle
	{
		private GameplayEffectContext m_data;

		public void GameplayEffectContext() { }
        public void GameplayEffectContext(GameplayEffectContext data)
		{
			m_data = data;
        }

		void Clear()
		{
			m_data.Reset();
        }

        bool IsValid()
        {
            return m_data.IsValid();
        }

		public GameplayEffectContext Get()
		{
			if (m_data != null) 
                return m_data;
			return null;
		}

		//todo-2
        public void AddSourceObject(Object newSourceObject)
        {
            if (IsValid())
                m_data.AddSourceObject(newSourceObject);
        }

        //todo-2
        Object GetSourceObject()
        {
            if (IsValid())
                return m_data.GetSourceObject();
            return null;
        }
    }
}
