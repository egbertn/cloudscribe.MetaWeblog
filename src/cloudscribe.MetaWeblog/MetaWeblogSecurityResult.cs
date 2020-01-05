namespace cloudscribe.MetaWeblog
{
    public class MetaWeblogSecurityResult
    {
        public MetaWeblogSecurityResult(
            string displayName,
            string blogId,
            bool isAuthenticated, 
            bool canEditPosts,
            bool canEditPages
            )
        {
            this.DisplayName = displayName;
            this.BlogId = blogId;
            this.IsAuthenticated = isAuthenticated;
            this.CanEditPosts = canEditPosts;
            this.CanEditPages = canEditPages;
        }

        public string DisplayName { get; }

		public string BlogId { get; }


		public bool IsAuthenticated { get; }

		public bool CanEditPosts { get; }

		public bool CanEditPages { get; }
	}
}
